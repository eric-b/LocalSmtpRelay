namespace LocalSmtpRelay.Components.Llm
{
    public sealed record ChatCompletionResponse(ChatCompletionChoices[] Choices);

    public sealed record ChatCompletionChoices(string finish_reason, ChatCompletionMessage message)
    {
        public bool HasCompleted() => finish_reason == "stop";
    }
}
