
namespace LocalSmtpRelay.Startup
{
    public sealed class SendMessageAtStartupOptions
    {
        public string? From { get; set; }
        public string? To { get; set; } 
        public string? Subject { get; set; }
        public string? Body { get; set; } 
    }
}
