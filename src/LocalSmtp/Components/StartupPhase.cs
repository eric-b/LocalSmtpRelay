using System;
using System.Threading.Tasks;
using System.Threading;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace LocalSmtpRelay.Components
{
    public sealed class StartupPhase
    {
        private readonly Startup.SendMessageAtStartupOptions _sendMsgAtStartup;
        private readonly MessageStore _store;
        private readonly ILogger<StartupPhase> _logger;

        public StartupPhase(StartupPhaseOptions options, MessageStore store, ILogger<StartupPhase> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sendMsgAtStartup = options.SendMessageAtStartupOptions;
        }

        public Task CatchUpStoredMessages(CancellationToken cancellationToken)
            => _store.CatchUpStoredMessages(cancellationToken);

        public async Task SmtpServerStarting(CancellationToken cancellationToken)
        {
            if (_sendMsgAtStartup.To != null && _sendMsgAtStartup.Subject != null)
            {
                _logger.LogInformation($"Sending startup notification to {_sendMsgAtStartup.To}");
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(MailboxAddress.Parse(_sendMsgAtStartup.From ?? _sendMsgAtStartup.To));
                    message.To.Add(MailboxAddress.Parse(_sendMsgAtStartup.To));
                    message.Subject = _sendMsgAtStartup.Subject;
                    message.Body = new TextPart("plain") { Text = !string.IsNullOrEmpty(_sendMsgAtStartup.Body) ? _sendMsgAtStartup.Body : $"Current time: {DateTimeOffset.Now}" };

                    await _store.SaveAsync(message, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception ex)
                {
                    // app will crash
                    throw new Exception($"Failed to send startup notification to {_sendMsgAtStartup.To}, from {_sendMsgAtStartup.From}.", ex);
                }
            }
        }
    }
}
