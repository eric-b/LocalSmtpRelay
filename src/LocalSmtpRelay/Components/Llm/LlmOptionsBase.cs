
namespace LocalSmtpRelay.Components.Llm
{
    public abstract class LlmOptionsBase
    {
        // See https://github.com/ggerganov/llama.cpp/blob/master/examples/server/README.md#post-completion-given-a-prompt-it-returns-the-predicted-completion
        public string? Model { get; set; }

        public double? Temperature { get; set; }

        public double? TopP { get; set; }

        public string? SystemPrompt { get; set; }
    }
}
