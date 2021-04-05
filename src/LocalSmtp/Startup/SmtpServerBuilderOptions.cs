
namespace LocalSmtpRelay.Startup
{
    sealed class SmtpServerBuilderOptions
    {
        public sealed class Port
        {
            public int Number { get; set; }
            
            public bool IsSecure { get; set; }

            public static implicit operator Port(int number) => new Port { Number = number };
        }

       

        public string? Hostname { get; set; }

        public Port[]? Ports { get; set; }

    }
}
