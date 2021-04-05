using System;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;
using MediatR;
using Nito.AsyncEx;

namespace LocalSmtpRelay.Components
{
    public sealed class SmtpForwarder : IAsyncDisposable
    {
        private readonly ILogger<SmtpForwarder> _logger;
        private readonly IOptionsMonitor<SmtpForwarderOptions> _options;
        private readonly SmtpClient _smtpClient;
        private readonly ICredentials? _smtpClientCredentials;
        private readonly BlockingCollection<FileInfo> _queue;
        private readonly Timer _smtpClientIdleTimer;
        private readonly TimeSpan _idleDelay;
        private readonly SemaphoreSlim _smtpClientSemaphore;
        private readonly Task _bgSendTask;
        private readonly IPublisher _publisher;

        private readonly ConcurrentQueue<FileInfo> _pendingMessagesFromSocketFailures;

        private bool _firstConnectError;
        private int _disposeCount;
        private bool _isFirstConnect = true;


        sealed class SmtpClientLogger : MailKit.IProtocolLogger
        {
            private readonly ILogger _logger;

            public SmtpClientLogger(ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public void Dispose() { }

            public void LogClient(byte[] buffer, int offset, int count)
                => _logger.LogDebug($"Sent {count} bytes");

            public void LogConnect(Uri uri)
                => _logger.LogInformation($"Connection to {uri}");

            public void LogServer(byte[] buffer, int offset, int count)
                => _logger.LogDebug($"Received {count} bytes");
        }

        public SmtpForwarder(IOptionsMonitor<SmtpForwarderOptions> options,
                             IPublisher publisher,
                             ILogger<SmtpForwarder> logger,
                             IHostApplicationLifetime appLifetime)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _smtpClient = new SmtpClient(new SmtpClientLogger(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.CurrentValue.Authentication != null)
                _smtpClientCredentials = CreateCredentials(_options.CurrentValue.Authentication);
            _pendingMessagesFromSocketFailures = new ConcurrentQueue<FileInfo>();
            
            _idleDelay = options.CurrentValue.AutoDisconnectAfterIdle ?? TimeSpan.FromMinutes(1);
            if (_idleDelay <= TimeSpan.Zero)
                _idleDelay = TimeSpan.FromMinutes(1);

            _smtpClientIdleTimer = new Timer(IdleTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            _smtpClientSemaphore = new SemaphoreSlim(1, 1);
            _queue = new BlockingCollection<FileInfo>(options.CurrentValue.MaxQueueLength ?? 5);
            _bgSendTask = Task.Factory.StartNew(() => BackgroundSendFromQueue(appLifetime.ApplicationStopping), appLifetime.ApplicationStopping, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async Task BackgroundSendFromQueue(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                        _disposeCount == 0)
                {
                    var file = _queue.Take(cancellationToken);
                    
                    await ProcessSend(file, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                    if (_firstConnectError)
                        return;
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    _logger.LogError(ex, $"{nameof(BackgroundSendFromQueue)} failed.");
            }
        }

        private async Task ProcessSend(FileInfo file, CancellationToken cancellationToken)
        {
            if (_disposeCount != 0)
                return;
            try
            {
                MimeMessage message = await MimeMessage.LoadAsync(file.FullName).ConfigureAwait(continueOnCapturedContext: false);
                string? defaultRecipient = _options.CurrentValue.DefaultRecipient;
                if (message.To.Count == 0)
                {
                    if (defaultRecipient != null)
                    {
                        _logger.LogDebug($"Set default recipient: To:{defaultRecipient} Subject:{message.Subject}");
                        message.To.Add(MailboxAddress.Parse(defaultRecipient));
                    }
                    else
                    {
                        _logger.LogWarning($"No default recipient for message: {message.Subject}");
                        return;
                    }
                }

                if (await _smtpClientSemaphore.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(continueOnCapturedContext: false))
                {
                    try
                    {
                        if (_disposeCount != 0)
                            return;

                        await Connect(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                        if (_options.CurrentValue.Disable)
                        {
                            _logger.LogWarning($"Message not sent because forwarder is disabled: To:{message.To} Subject:{message.Subject}");
                            return;
                        }
                        await _smtpClient.SendAsync(message).ConfigureAwait(continueOnCapturedContext: false);
                        if (!Utility.TryDeleteFile(file))
                            _logger.LogWarning($"Failed to delete: {file}");

                        _logger.LogInformation($"{nameof(SmtpForwarder)} sent message to {message.To}: {message.Subject}");
                        if (!_smtpClientIdleTimer.Change(_idleDelay, Timeout.InfiniteTimeSpan))
                        {
                            // Should not happen
                            await _smtpClient.DisconnectAsync(quit: true).ConfigureAwait(continueOnCapturedContext: false);
                        }
                    }
                    finally
                    {
                        _smtpClientSemaphore.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send message {file}");
                if (ex is SocketException)
                {
                    _pendingMessagesFromSocketFailures.Enqueue(file);
                    try
                    {
                        // _smtpClientIdleTimer used for short time catch-up
                        _smtpClientIdleTimer.Change(_idleDelay, Timeout.InfiniteTimeSpan);
                    }
                    catch
                    { }
                }
            }
        }

        private void IdleTimerCallback(object? state)
        {
            if (_disposeCount != 0)
                return;

            // Best effort to process _pendingMessagesFromSocketFailures in a timely manner
            if (_smtpClientSemaphore.CurrentCount == 0)
            {
                try
                {
                    _smtpClientIdleTimer.Change(_idleDelay, Timeout.InfiniteTimeSpan);
                    return;
                }
                catch
                {
                }
            }

            int catchUpCount = 0;
            while (_pendingMessagesFromSocketFailures.TryDequeue(out FileInfo? file))
            {
                try
                {
                    _logger.LogInformation($"New attempt: {file.Name}");
                    AsyncContext.Run(() => ProcessSend(file, default));
                    catchUpCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed new send attempt: {file.Name}");
                }
            }
            if (catchUpCount != 0)
                return;

            if (_smtpClientSemaphore.Wait(0))
            {
                try
                {
                    if (_disposeCount != 0)
                        return;

                    _smtpClient.Disconnect(quit: true);
                    _logger.LogInformation("Auto disconnect");
                }
                finally
                {
                    _smtpClientSemaphore.Release();
                }
            }
            else
            {
                try
                {
                    _smtpClientIdleTimer.Change(_idleDelay, Timeout.InfiniteTimeSpan);
                }
                catch
                {
                }
            }
        }

        public void Enqueue(FileInfo message, CancellationToken cancellationToken)
        {
            if (_disposeCount != 0)
                throw new ObjectDisposedException(nameof(SmtpForwarder));

            _queue.Add(message, cancellationToken);
        }

        public async Task WaitForQueueEmpty(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_queue.Count == 0)
                    break;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public bool TryEnqueue(FileInfo message)
        {
            if (_disposeCount != 0)
                return false;

            try
            {
                return _queue.TryAdd(message);
            }
            catch 
            {
                return false;
            }
        }

        
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposeCount) != 1)
                return;
            _queue.CompleteAdding();

            await _bgSendTask.ConfigureAwait(continueOnCapturedContext: false);

            if (await _smtpClientSemaphore.WaitAsync(TimeSpan.FromMinutes(2)).ConfigureAwait(continueOnCapturedContext: false))
            {
                try
                {
                    await _smtpClientIdleTimer.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);

                    if (_smtpClient.IsConnected)
                    {
                        await _smtpClient.DisconnectAsync(quit: true).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    _smtpClient.Dispose();
                }
                finally 
                {
                    _smtpClientSemaphore.Release(); 
                }
            }

            
            _bgSendTask.Dispose();
            _smtpClientSemaphore.Dispose();
            _queue.Dispose();
            _logger.LogInformation($"{nameof(SmtpForwarder)} disposed");
        }

        private async Task Connect(CancellationToken cancellationToken)
        {
            if (!_smtpClient.IsConnected)
            {
                SmtpForwarderOptions options = _options.CurrentValue;
                try
                {

                    await _smtpClient.ConnectAsync(options.Hostname, options.Port ?? 25, options.EnableSsl, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    _isFirstConnect = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to connect to {options.Hostname}:{options.Port ?? 25}.");
                    if (ex is not SocketException && _isFirstConnect)
                    {
                        _firstConnectError = true;
                        await _publisher.Publish(new Model.SmtpForwarderFailed(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    throw;
                }
                if (_smtpClientCredentials != null)
                {
                    try
                    {
                        await _smtpClient.AuthenticateAsync(_smtpClientCredentials, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }
                    catch (AuthenticationException authException)
                    {
                        _logger.LogError(authException, $"Authentication to {options.Hostname} failed. Check settings.");
                        _firstConnectError = true;
                        await _publisher.Publish(new Model.SmtpForwarderFailed(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
            }
        }

        private static NetworkCredential CreateCredentials(SmtpForwarderOptions.AuthenticationParameters parameters)
        {
            if (parameters is null)
                throw new ArgumentNullException(nameof(parameters));
            if (!string.IsNullOrEmpty(parameters.PasswordFile))
            {
                if (!File.Exists(parameters.PasswordFile))
                    throw new FileNotFoundException($"{nameof(parameters.PasswordFile)} not found.", parameters.PasswordFile);

                using (var fs = File.Open(parameters.PasswordFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[1];
                    var secret = new SecureString();
                    while (fs.Read(buffer, 0, 1) == 1)
                    {
                        secret.AppendChar((char)buffer[0]);
                    }
                    secret.MakeReadOnly();
                    return new NetworkCredential(parameters.Username, secret);
                }
            }
            return new NetworkCredential(parameters.Username, parameters.Password);
        }
    }
}
