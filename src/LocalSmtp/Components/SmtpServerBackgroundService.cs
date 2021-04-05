using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmtpServer;
using SmtpSrv = SmtpServer.SmtpServer;

namespace LocalSmtpRelay.Components
{
    sealed class SmtpServerBackgroundService : BackgroundService
    {
        private readonly SmtpSrv _server;
        private readonly ILogger<SmtpServerBackgroundService> _logger;
        private readonly StartupPhase _startupPhase;

        public SmtpServerBackgroundService(SmtpSrv server,
                                           StartupPhase startupPhase,
                                           ILogger<SmtpServerBackgroundService> logger)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _startupPhase = startupPhase ?? throw new ArgumentNullException(nameof(startupPhase));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _server.SessionCreated += OnSessionCreated;
            _server.SessionCancelled += OnSessionCancelled;
            _server.SessionCompleted += OnSessionCompleted;
            _server.SessionFaulted += OnSessionFaulted;
        }

        public override void Dispose()
        {
            _server.SessionCreated -= OnSessionCreated;
            _server.SessionCancelled -= OnSessionCancelled;
            _server.SessionCompleted -= OnSessionCompleted;
            _server.SessionFaulted -= OnSessionFaulted;
            base.Dispose();
            _logger.LogInformation($"{nameof(SmtpServerBackgroundService)} disposed.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _startupPhase.CatchUpStoredMessages(stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
            await _startupPhase.SmtpServerStarting(stoppingToken).ConfigureAwait(continueOnCapturedContext: false);

            _logger.LogInformation($"Starting SMTP server");
            await _server.StartAsync(stoppingToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        private void OnSessionCreated(object? sender, SessionEventArgs e)
        {
            _logger.LogDebug($"Session created");
        }

        private void OnSessionFaulted(object? sender, SessionFaultedEventArgs e)
        {
            // Debug and not Error: allows to easily ignore these noisy messages.
            _logger.LogDebug($"Session faulted (user: {e.Context.Authentication.User}) - {e.Exception}");
        }

        private void OnSessionCompleted(object? sender, SessionEventArgs e)
        {
            _logger.LogInformation($"Session completed (user: {e.Context.Authentication.User})");
        }

        private void OnSessionCancelled(object? sender, SessionEventArgs e)
        {
            _logger.LogInformation($"Session cancelled (user: {e.Context.Authentication.User})");
        }
    }
}
