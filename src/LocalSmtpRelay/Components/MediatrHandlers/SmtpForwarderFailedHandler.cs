using LocalSmtpRelay.Model;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.MediatrHandlers
{
    public sealed class SmtpForwarderFailedHandler : INotificationHandler<SmtpForwarderFailed>
    {
        private readonly MessageStore _store;

        public SmtpForwarderFailedHandler(MessageStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Handle(SmtpForwarderFailed notification, CancellationToken cancellationToken)
            => _store.Fail();
    }
}
