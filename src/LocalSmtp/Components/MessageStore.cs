using System;
using System.Linq;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using LocalSmtpRelay.Model;

namespace LocalSmtpRelay.Components
{
    public sealed class MessageStore : IMessageStore 
    {
        static class SmtpResponses
        {
            public static readonly SmtpResponse Ok = SmtpResponse.Ok;
            public static readonly SmtpResponse ServiceUnavailable = new SmtpResponse(SmtpReplyCode.ServiceUnavailable);
            public static readonly SmtpResponse MailboxUnavailable = SmtpResponse.MailboxUnavailable;
            public static readonly SmtpResponse SizeLimitExceeded = SmtpResponse.SizeLimitExceeded;
            public static readonly SmtpResponse BadEmailAddress = new SmtpResponse(SmtpReplyCode.BadEmailAddress);
        }

        private readonly ILogger<MessageStore> _logger;
        private readonly ISender _inprocSender;
        private readonly IOptionsMonitor<MessageStoreOptions> _options;
        private bool _failed;

        public MessageStore(IOptionsMonitor<MessageStoreOptions> options, ISender inprocSender, ILogger<MessageStore> logger)
        {
            _inprocSender = inprocSender ?? throw new ArgumentNullException(nameof(inprocSender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            logger.LogInformation($"Store directory: {_options.CurrentValue.Directory}");
        }

        public async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            if (_failed)
                return SmtpResponses.ServiceUnavailable;

            MessageStoreOptions currentOptions = _options.CurrentValue;
            if (buffer.Length > currentOptions.MaxMessageLengthBytes)
            {
                _logger.LogWarning($"{nameof(SmtpResponses.SizeLimitExceeded)}: {buffer.Length} bytes exceeds {currentOptions.MaxMessageLengthBytes}. Destination: {string.Join(", ", transaction.To.Select(t => $"{t.User}@{t.Host}"))}");
                return SmtpResponses.SizeLimitExceeded;
            }

            string[]? addrWhitelist = currentOptions.DestinationAddressWhitelist;
            if (addrWhitelist?.Length > 0)
            {
                foreach (var to in transaction.To)
                {
                    if (!addrWhitelist.Contains($"{to.User}@{to.Host}"))
                    {
                        _logger.LogWarning($"{nameof(SmtpResponses.MailboxUnavailable)}: at least one destination is not included in whitelist: {string.Join(", ", transaction.To.Select(t => $"{t.User}@{t.Host}"))}");
                        return SmtpResponses.MailboxUnavailable;
                    }
                }
                if (currentOptions.RejectEmptyRecipient && transaction.To.Count == 0)
                {
                    _logger.LogWarning($"{nameof(SmtpResponses.BadEmailAddress)}: at least one recipient must be specified ({context.Authentication?.User}).");
                    return SmtpResponses.BadEmailAddress;
                }
            }

            string fileId = Guid.NewGuid().ToString() + ".mime";
            var filepath = Path.Combine(currentOptions.Directory, $"{transaction.From.User}@{transaction.From.Host}", fileId);
            _logger.LogDebug($"Storing message {filepath}");
            var file = new FileInfo(filepath);
            file.Directory!.Create();
            using (FileStream fs = file.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                SequencePosition position = buffer.GetPosition(0);
                while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                {
                    await fs.WriteAsync(memory, cancellationToken);
                }
            }

            SendResponse response = await _inprocSender.Send(new SendRequest(file), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (response.IsSuccess)
            {
                return SmtpResponses.Ok;
            }
            else
            {
                MimeMessage message = await MimeMessage.LoadAsync(file.FullName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                _logger.LogWarning($"Too many requests - Message not sent: {message.From} -> {message.To} ({message.Subject}, {fileId})");
                if (currentOptions.DeleteRejectedMessages)
                    Utility.TryDeleteFile(file);

                return SmtpResponses.ServiceUnavailable;
            }
        }

        public async Task SaveAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            if (_failed) // should not happen here, but kept for consistency
                return;

            MessageStoreOptions currentOptions = _options.CurrentValue;
            string fileId = Guid.NewGuid().ToString() + ".mime";
            var filepath = Path.Combine(currentOptions.Directory, ((MailboxAddress)message.From[0]).Address, fileId);
            _logger.LogDebug($"Storing message {filepath}");
            var file = new FileInfo(filepath);
            file.Directory!.Create();
            await message.WriteToAsync(filepath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            SendResponse response = await _inprocSender.Send(new SendRequest(file), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            if (!response.IsSuccess)
            {
                _logger.LogWarning($"Message not sent: {message.From} -> {message.To} ({message.Subject}, {fileId})");
                if (currentOptions.DeleteRejectedMessages)
                    Utility.TryDeleteFile(file);
            }
        }

        public Task Fail()
        {
            _failed = true;
            _logger.LogWarning("Fail fast following previous error.");
            return Task.CompletedTask;
        }

        public async Task CatchUpStoredMessages(CancellationToken cancellationToken)
        {
            MessageStoreOptions currentOptions = _options.CurrentValue;
            if (currentOptions.CatchUpOnStartup && Directory.Exists(currentOptions.Directory))
            {
                try
                {
                    FileInfo[] files = new DirectoryInfo(currentOptions.Directory).GetFiles("*.mime", SearchOption.AllDirectories);
                    if (files.Length != 0 && !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation($"Processing messages stored from previous session...");
                        await _inprocSender.Send(new SendCatchUpRequest(files), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                        _logger.LogError(ex, $"Failed to process existing messages stored from previous application session.");
                }
            }
        }
    }
}
