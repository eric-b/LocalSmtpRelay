
namespace LocalSmtpRelay.Startup
{
    static class AppSettings
    {
        public static class Sections
        {
            public const string SmtpServer = "SmtpServer";
            public const string MessageStore = "MessageStore";
            public const string SmtpForwarder = "SmtpForwarder";
            public const string SmtpForwarderLlmParameters = "SmtpForwarder:LlmEnrichment";
            public const string SendMessageAtStartup = "SendMessageAtStartup";
            public const string AlertManagerForwarder = "AlertManagerForwarder";
            public const string LlmClient = "LlmClient";
        }
    }
}
