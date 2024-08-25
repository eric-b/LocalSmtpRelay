using System;
using System.IO;
using MediatR;

namespace LocalSmtpRelay.Model
{
    public sealed class SendCatchUpRequest : IRequest<SendResponse>
    {
        public FileInfo[] Files { get; }

        public SendCatchUpRequest(FileInfo[] files)
        {
            Files = files ?? throw new ArgumentNullException(nameof(files));
        }
    }
}
