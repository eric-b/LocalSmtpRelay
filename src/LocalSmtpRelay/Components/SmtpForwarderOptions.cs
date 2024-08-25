using FluentValidation;
using System;

namespace LocalSmtpRelay.Components
{
    public sealed class SmtpForwarderOptions
    {
        public string? DefaultRecipient { get; set; }

        public string Hostname { get; set; } = default!;

        public bool EnableSsl { get; set; }

        public int? Port { get; set; }

        public AuthenticationParameters? Authentication { get; set; }

        public int? MaxQueueLength { get; set; }

        public TimeSpan? AutoDisconnectAfterIdle { get; set; }

        public bool Disable { get; set; }

        public class AuthenticationParameters
        {
            public string Username { get; set; } = default!;

            public string? Password { get; set; }

            public string? PasswordFile { get; set; }

            public sealed class Validator : AbstractValidator<AuthenticationParameters?>
            {
                public Validator()
                {
                    RuleFor(option => option!.Username).NotEmpty();
                    RuleFor(option => option!.Password).NotEmpty().When(option => string.IsNullOrEmpty(option!.PasswordFile));
                }
            }
        }

        public sealed class Validator : AbstractValidator<SmtpForwarderOptions>
        {
            public Validator()
            {
                RuleFor(option => option.DefaultRecipient).EmailAddress();
                RuleFor(option => option.Hostname).NotEmpty();
                RuleFor(option => option.Authentication).SetValidator(new AuthenticationParameters.Validator());
            }
        }
    }
}
