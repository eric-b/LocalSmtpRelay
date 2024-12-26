using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.Llm
{
    public abstract class LlmHelperBase(LlmChatClient llmChat, ILogger logger)
    {
        private bool _llmStatusOk;

        public static class Defaults
        {
            public const string Model = "OpenAiModel";
            public const string SystemRole = "system";
            public const string UserRole = "user";
        }

        private async Task<bool> IsLlmReady(CancellationToken cancellationToken)
        {
            if (_llmStatusOk)
                return true;

            var status = await llmChat.GetStatus(cancellationToken);
            if (status != null)
            {
                if (status.IsOk())
                {
                    _llmStatusOk = true;
                    logger.LogInformation("LLM is ready: {Status}", JsonSerializer.Serialize(status));
                }
                else
                {
                    logger.LogWarning("LLM is not ready: {Status}", JsonSerializer.Serialize(status));
                }
            }
            return _llmStatusOk;
        }

        protected async Task<string?> Complete(ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (!await IsLlmReady(cancellationToken))
                    return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LLM is not ready.");
                return null;
            }

            try
            {
                return await llmChat.Complete(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _llmStatusOk = false;
                logger.LogError(ex, "Failed to query LLM.");
            }
            return null;
        }
    }
}
