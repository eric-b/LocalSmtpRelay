
namespace LocalSmtpRelay.Startup
{
    static class AppSettings
    {
        public static class Sections
        {
            public const string SmtpServer = "SmtpServer";
            public const string MessageStore = "MessageStore";
            public const string SmtpForward = "SmtpForwarder";
            public const string SendMessageAtStartup = "SendMessageAtStartup";
        }
    }
}
