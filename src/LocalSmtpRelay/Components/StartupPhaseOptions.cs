using Microsoft.Extensions.Options;
using LocalSmtpRelay.Startup;

namespace LocalSmtpRelay.Components
{
    public sealed class StartupPhaseOptions
    {
        public MessageStoreOptions MessageStoreOptions { get; }

        public SendMessageAtStartupOptions SendMessageAtStartupOptions { get; }

        public StartupPhaseOptions(IOptions<MessageStoreOptions> storeOptions, IOptions<SendMessageAtStartupOptions> sendOnStartupOptions)
        {
            MessageStoreOptions = storeOptions.Value;
            SendMessageAtStartupOptions = sendOnStartupOptions.Value;
        }
    }
}
