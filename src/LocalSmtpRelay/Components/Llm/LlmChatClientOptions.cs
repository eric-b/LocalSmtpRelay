using System;

namespace LocalSmtpRelay.Components.Llm
{
    public sealed class LlmChatClientOptions
    {
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// May be necessary if server is not Llama.cpp (for example OpenAi API).
        /// </summary>
        public bool DisableLlmHealthCheck { get; set; }

        /// <summary>
        /// May be necessary for OpenAi API.
        /// </summary>
        public string? ApiKey { get; set; }

        public TimeSpan? RequestTimeout { get; set; }

        public LlmChatParameters Parameters { get; } = new();
    }

    public sealed class LlmChatParameters : LlmOptionsBase
    {
    }
}
