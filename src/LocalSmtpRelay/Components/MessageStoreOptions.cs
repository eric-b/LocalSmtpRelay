using FluentValidation;

namespace LocalSmtpRelay.Components
{
    public sealed class MessageStoreOptions
    {
        public string Directory { get; set; } = default!;

        public bool DeleteRejectedMessages { get; set; }

        public bool CatchUpOnStartup { get; set; }

        public string[]? DestinationAddressWhitelist { get; set; }

        public int? MaxMessageLengthBytes { get; set; }

        public bool RejectEmptyRecipient { get; set; }

        public sealed class Validator : AbstractValidator<MessageStoreOptions>
        {
            public Validator()
            {
                RuleFor(option => option.Directory).NotEmpty();
                RuleForEach(option => option.DestinationAddressWhitelist).EmailAddress();
            }
        }
    }
}
