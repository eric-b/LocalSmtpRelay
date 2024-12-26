using LocalSmtpRelay.Model;
using System;

namespace LocalSmtpRelay.Components.AlertManager
{
    public sealed class AlertManagerForwarderOptions
    {
        public bool Disable { get; set; }

        public Uri? BaseUrl { get; set; }

        public MessageRule[] MessageRules { get; set; } = [];
    }

    public sealed class MessageRule
    {
        public string? AlertName { get; set; }

        public Uri? GeneratorUrl { get; set; }

        public MessageField RegexOnField { get; set; }

        public string? AlertRegex { get; set; }

        public string? ResolutionRegex { get; set; }

        public string[] Labels { get; set; } = [];

        public string[] Annotations { get; set; } = [];

        public TimeSpan? ResolutionTimeout { get; set; }

        public bool EvaluateOtherRules { get; set; }

        public bool RunLlmOnBody { get; set; }

        public string? UserPrompt { get; set; }
    }
}
