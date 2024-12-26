using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.AlertManager
{
    public sealed class AlertManagerClient(HttpClient httpClient, ILogger<AlertManagerClient> logger)
    {
        // Minimalistic alertmanager client based on https://github.com/prometheus/alertmanager/blob/main/api/v2/openapi.yaml

        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task<Status?> GetStatus(CancellationToken cancellationToken)
        {
            using var response = await httpClient.GetAsync("api/v2/status", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.OK && 
                response.Content?.Headers.ContentType?.MediaType == "application/json")
            {
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var status = JsonSerializer.Deserialize<Status>(json, JsonSerializerOptions);
                if (status?.VersionInfo != null)
                    return status;
            }
            logger.LogError("Could not get Alertmanager status: GET {URL} {HttpStatus}", response.RequestMessage?.RequestUri?.ToString(), (int)response.StatusCode);
            return null;
        }

        public async Task<bool> SendAlert(Alert alert, CancellationToken cancellationToken)
        {
            string singleAlertJson = JsonSerializer.Serialize(alert, JsonSerializerOptions);
            using var response = await httpClient.PostAsync("api/v2/alerts", new StringContent("[ " + singleAlertJson + " ]", Encoding.UTF8, "application/json"), cancellationToken);
            bool success = response.StatusCode == System.Net.HttpStatusCode.OK;
            if (success)
            {
                logger.LogDebug("Sent alert: {Json}", singleAlertJson);
            }
            else
            {
                logger.LogError("Could not send alert: POST {URL} {HttpStatus}", response.RequestMessage?.RequestUri?.ToString(), (int)response.StatusCode);
            }
            return success;
        }
    }
}
