namespace LocalSmtpRelay.Components.Llm
{
    public sealed record ChatCompletionRequest(string Model, ChatCompletionMessage[] Messages, double? temperature, double? top_p);
}
