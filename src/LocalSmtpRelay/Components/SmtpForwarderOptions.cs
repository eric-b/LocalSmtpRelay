using FluentValidation;
using LocalSmtpRelay.Model;
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
        
        public Llm LlmEnrichment { get; } = new();

        public VoidFilter Void { get; } = new();

        public class AuthenticationParameters
        {
            public string Username { get; set; } = default!;

            public string? Password { get; set; }

            public string? PasswordFile { get; set; }

            public sealed class AuthenticationParametersValidator : AbstractValidator<AuthenticationParameters?>
            {
                public AuthenticationParametersValidator()
                {
                    RuleFor(option => option!.Username).NotEmpty();
                    RuleFor(option => option!.Password).NotEmpty().When(option => string.IsNullOrEmpty(option!.PasswordFile));
                }
            }
        }

        public sealed class Llm
        {
            /// <summary>
            /// If set, takes precedence over default prompt.
            /// </summary>
            public string? UserPrompt { get; set; }

           
            public LlmRule[] Rules { get; set; } = [];
        }

        public sealed class VoidFilter 
        {
            public VoidMatcherRule[] Matchers { get; set; } = [];
        }

        public sealed class VoidMatcherRule
        {
            /// <summary>
            /// If regex matches, message will be ignored.
            /// </summary>
            public string Regex { get; set; } = default!;

            public MessageField RegexOnField { get; set; }
        }

        public sealed class LlmRule
        {
            /// <summary>
            /// Regex must match to apply LLM on message body to set subject.
            /// </summary>
            public string Regex { get; set; } = default!;

            public MessageField RegexOnField { get; set; }

            /// <summary>
            /// Subject prefix (appended with LLM result).
            /// </summary>
            public string? SubjectPrefix { get; set; }

            /// <summary>
            /// If set, takes precedence over <see cref="Llm.UserPrompt"/>.
            /// </summary>
            public string? UserPrompt { get; set; }
        }

        public sealed class Validator : AbstractValidator<SmtpForwarderOptions>
        {
            public Validator()
            {
                RuleFor(option => option.DefaultRecipient).EmailAddress();
                RuleFor(option => option.Hostname).NotEmpty();
                RuleFor(option => option.Authentication).SetValidator(new AuthenticationParameters.AuthenticationParametersValidator());
            }
        }
    }
}
