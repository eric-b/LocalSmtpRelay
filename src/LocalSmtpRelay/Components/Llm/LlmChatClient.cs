using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.Llm
{
    /// <summary>
    /// Client based on documentation of endpoint "/v1/chat/completions"
    /// from Llama.cpp (OpenAI-compatible Chat Completions API).
    /// </summary>
    /// <param name="options"></param>
    /// <param name="httpClient"></param>
    /// <param name="logger"></param>
    public sealed class LlmChatClient(IOptions<LlmChatClientOptions> options, HttpClient httpClient, ILogger<LlmChatClient> logger)
    {
        private readonly bool _disableHealthCheck = options.Value.DisableLlmHealthCheck;

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task<HealthStatus?> GetStatus(CancellationToken cancellationToken)
        {
            if (_disableHealthCheck)
                return HealthStatus.ForceOk;

            using var response = await httpClient.GetAsync("health", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.OK && 
                response.Content?.Headers.ContentType?.MediaType == "application/json")
            {
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var status = JsonSerializer.Deserialize<HealthStatus>(json, JsonSerializerOptions);
                if (status?.Status != null)
                    return status;
            }
            logger.LogError("Could not get LLM server status: GET {URL} {HttpStatus}", response.RequestMessage?.RequestUri?.ToString(), (int)response.StatusCode);
            return null;
        }

        public async Task<string?> Complete(ChatCompletionRequest completionRequest, CancellationToken cancellationToken)
        {
            string? completion = null;
            var json = JsonSerializer.Serialize(completionRequest, JsonSerializerOptions);
            logger.LogDebug("Requesting LLM... this can take a while.");
            using var response = await httpClient.PostAsync("v1/chat/completions", new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
            bool success = response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null;
            if (success)
            {
                string responseStr = await response.Content!.ReadAsStringAsync(cancellationToken);
                if (responseStr != null)
                {
                    var completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseStr, JsonSerializerOptions);
                    if (completionResponse != null && completionResponse.Choices?.Length != 0)
                    {
                        var completionMessage = completionResponse.Choices![0];
                        if (completionMessage.HasCompleted())
                        {
                            completion = completionMessage.message.Content;
                        }
                    }
                }
            }
            else
            {
                logger.LogError("Could not request LLM: POST {URL} {HttpStatus}", response.RequestMessage?.RequestUri?.ToString(), (int)response.StatusCode);
            }

            return completion;
        }
    }
}
