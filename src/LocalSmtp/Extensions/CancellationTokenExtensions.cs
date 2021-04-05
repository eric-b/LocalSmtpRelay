using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Extensions
{
    static class CancellationTokenExtensions
    {
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            // Source: https://github.com/dotnet/runtime/issues/14991#issuecomment-131221355
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>?)s)?.SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
