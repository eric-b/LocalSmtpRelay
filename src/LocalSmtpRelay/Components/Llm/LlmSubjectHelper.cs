using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.Llm
{
    public sealed class LlmSubjectHelper(IOptions<LlmSubjectHelperOptions> options, LlmChatClient llmChat, ILogger<LlmSubjectHelper> logger)
        : LlmHelperBase(llmChat, logger)
    {
        static class PromptTemplate
        {
            /// <summary>
            /// "%1" will be replaced by message body.
            /// </summary>
            private const string UserTemplate =
    """"
### Instruction:

Given the following email body, return me the appropriate email subject in the same language as the email body. Respond just with the subject value without header:

%1

### Response:

"""";

            public static string GetPrompt(string? userTemplate, string messageContent) => userTemplate ?? UserTemplate.Replace("%1", messageContent);
        }

        private static readonly ChatCompletionMessage DefaultSystemInstructions = new(Defaults.SystemRole, "You respond directly to user's instructions. Your responses are always made of a single short sentence of less than 45 characters, without introductory text, without any text formatting. Do not add any note at the end of your response.");

        private readonly ChatCompletionRequest requestPrototype = new(
            options.Value.Model ?? Defaults.Model,
            [
                !string.IsNullOrEmpty(options.Value.SystemPrompt) ? new(Defaults.SystemRole, options.Value.SystemPrompt) : DefaultSystemInstructions,
                new(Defaults.UserRole, string.Empty)
            ],
            options.Value.Temperature,
            options.Value.TopP
            );

        public async Task<string?> TryGetSubject(string? promptTemplate, string messageTextBody, CancellationToken cancellationToken)
        {
            var prompt = PromptTemplate.GetPrompt(promptTemplate, messageTextBody);
            var completionRequest = requestPrototype with { Messages = [requestPrototype.Messages[0], new(Defaults.UserRole, prompt)] };

            var completion = await Complete(completionRequest, cancellationToken);
            if (completion != null)
            {
                if (completion.Length < messageTextBody.Length)
                {
                    return completion;
                }
                else
                {
                    logger.LogWarning("Discarding response from LLM that is larger than content to summarize. This likely is a configuration and prompt issue.");
                }
            }
            return null;
        }
    }
}
