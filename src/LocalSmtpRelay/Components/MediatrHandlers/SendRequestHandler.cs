using System.IO;
using System.Threading.Tasks;
using System.Threading;
using MediatR;
using LocalSmtpRelay.Model;

namespace LocalSmtpRelay.Components.MediatrHandlers
{
    public sealed class SendRequestHandler : IRequestHandler<SendRequest, SendResponse>, IRequestHandler<SendCatchUpRequest, SendResponse>
    {
        private readonly SmtpForwarder _smtpForward;

        public SendRequestHandler(SmtpForwarder smtpForward)
        {
            _smtpForward = smtpForward ?? throw new System.ArgumentNullException(nameof(smtpForward));
        }

        public Task<SendResponse> Handle(SendRequest request, CancellationToken cancellationToken)
        {
            bool success = _smtpForward.TryEnqueue(request.File);
            return Task.FromResult(new SendResponse(success));
        }

        public async Task<SendResponse> Handle(SendCatchUpRequest request, CancellationToken cancellationToken)
        {
            if (request.Files.Length != 0)
            {
                foreach (FileInfo file in request.Files)
                {
                    _smtpForward.Enqueue(file, cancellationToken);
                }

                await _smtpForward.WaitForQueueEmpty(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                return new SendResponse(true);
            }

            return new SendResponse(false);
        }
    }
}
