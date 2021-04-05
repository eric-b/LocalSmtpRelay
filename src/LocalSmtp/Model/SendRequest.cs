using System;
using System.IO;
using MediatR;

namespace LocalSmtpRelay.Model
{
    public sealed class SendRequest : IRequest<SendResponse>
    {
        public FileInfo File { get; }

        public SendRequest(FileInfo file)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
        }
    }
}
