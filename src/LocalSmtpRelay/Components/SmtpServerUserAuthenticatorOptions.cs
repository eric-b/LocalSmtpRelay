
namespace LocalSmtpRelay.Components
{
    public sealed class SmtpServerUserAuthenticatorOptions
    {
        public sealed class Account
        {
            public string Username { get; set; } = default!;

            public string? Password { get; set; }

            public string? PasswordFile { get; set; }
        }

        public Account[]? Accounts { get; set; }

        public bool AllowAnonymous { get; set; }
    }
}
