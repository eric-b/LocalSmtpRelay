using LocalSmtpRelay.Helpers;
using LocalSmtpRelay.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LocalSmtpRelay.Components.AlertManager
{
    public sealed class AlertManagerForwarder(IOptions<AlertManagerForwarderOptions> options,
                                              AlertManagerClient alertManagerClient,
                                              LlmAlertSummarizer llm,
                                              ILogger<AlertManagerForwarder> logger)
    {
        private readonly bool _disable = options.Value.Disable;
        private readonly MessageRule[] _rules = options.Value.MessageRules ?? [];
        private readonly Dictionary<string, int> defaultAlertNumberBySubject = new(StringComparer.OrdinalIgnoreCase);

        private bool _amStatusOk;
        
        /// <summary>
        /// If message must be forwarded to Alertmanager, convert it to and send alert.
        /// If not or if it failed, return <c>false</c> for normal SMTP send.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns><c>true</c> if an alert has been successfully sent to Alertmanager.</returns>
        public async Task<bool> TrySendAlert(MimeMessage message, CancellationToken cancellationToken)
        {
            try
            {
                if (!await IsAlertManagerReady(cancellationToken))
                    return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alertmanager is not ready.");
                return false;
            }

            var alert = await MapToAlert(message, cancellationToken);
            if (alert is null)
                return false;

            try
            {
                return await alertManagerClient.SendAlert(alert, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send alert to Alertmanager: {Subject}", message.Subject);
                _amStatusOk = false;
                return false;
            }
        }

        private async Task<Alert?> MapToAlert(MimeMessage message, CancellationToken cancellationToken)
        {
            if (_disable || ContainsLocalSmtpLabel(message))
                return null;

            string? textBody = message.TextBody;
            Alert? alert = null;
            bool runLlm = false;
            string? promptTemplate = null;
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (string.IsNullOrEmpty(rule.AlertRegex))
                {
                    // Catch-all rule
                    if (alert is null) 
                    { 
                        alert = new Alert();
                    }

                    runLlm |= rule.RunLlmOnBody;
                    if (!string.IsNullOrEmpty(rule.UserPrompt))
                        promptTemplate = rule.UserPrompt;
                    SetAlertProperties(alert, rule);
                    
                    if (!rule.EvaluateOtherRules)
                        break;
                }
                else
                {
                    var input = string.Empty;
                    if (rule.RegexOnField.HasFlag(MessageField.Subject))
                        input += message.Subject;
                    if (rule.RegexOnField.HasFlag(MessageField.Body))
                        input += $"\r\n{textBody}";

                    if (rule.ResolutionRegex != null &&
                        RegexHelper.IsMatch(input, rule.ResolutionRegex))
                    {
                        if (alert is null)
                            alert = new Alert();

                        runLlm |= rule.RunLlmOnBody;
                        if (!string.IsNullOrEmpty(rule.UserPrompt))
                            promptTemplate = rule.UserPrompt;
                        SetAlertProperties(alert, rule);
                        alert.SetEndsAt(message.Date.UtcDateTime);

                        if (!rule.EvaluateOtherRules)
                            break;
                    }
                    else if (RegexHelper.IsMatch(input, rule.AlertRegex))
                    {
                        if (alert is null)
                            alert = new Alert();

                        runLlm |= rule.RunLlmOnBody;
                        if (!string.IsNullOrEmpty(rule.UserPrompt))
                            promptTemplate = rule.UserPrompt;
                        SetAlertProperties(alert, rule);

                        if (rule.ResolutionRegex != null && rule.ResolutionTimeout != null)
                            alert.SetEndsAt(message.Date.UtcDateTime.Add(rule.ResolutionTimeout.Value));

                        if (!rule.EvaluateOtherRules)
                            break;
                    }
                }
            }

            if (alert != null)
            {
                // set defaults
                if (!alert.HasName())
                {
                    if (!defaultAlertNumberBySubject.TryGetValue(message.Subject, out var hash))
                    {
                        using var murmur32 = new Murmur.Murmur32ManagedX86();
                        byte[] buffer = murmur32.ComputeHash(Encoding.UTF8.GetBytes(message.Subject));
                        hash = BitConverter.ToInt32(buffer);
                        defaultAlertNumberBySubject[message.Subject] = hash;
                    }
                    alert.SetName($"localsmtprelay-alert-{hash:X2}");
                }
                
                if (!alert.HasSummary())
                    alert.Annotations["summary"] = message.Subject;

                if (!alert.HasDescription())
                {
                    if (textBody != null)
                    {
                        string? llmSummary = null;
                        if (runLlm)
                        {
                            llmSummary = await llm.TrySummarize(promptTemplate, textBody, cancellationToken);
                            if (llmSummary != null)
                                textBody = llmSummary;
                        }
                        alert.Annotations["description"] = textBody;
                        if (llmSummary != null)
                        {
                            alert.Annotations["llm-enabled"] = "1";
                        }
                        
                    }
                    else
                    {
                        logger.LogInformation("MIME Message does not contain a plain text body. You probably should configure a default description annotation for Alertmanager: {Subject}", message.Subject);
                    }
                }
                
                alert.Labels["forwarded-by"] = "localsmtprelay";
            }

            return alert;
        }

        private bool ContainsLocalSmtpLabel(MimeMessage message)
        {
            string? textBody = message.TextBody;
            if (textBody != null)
            {
                const int indexNotFound = -1;
                var index = textBody.IndexOf("forwarded-by");
                if (index != indexNotFound && textBody.IndexOf("localsmtprelay", index) != indexNotFound)
                {
                    // avoid loops between alertmanager and localsmtprelay thanks to label "forwarded-by":"localsmtprelay"
                    return true;
                }
            }

            if (message.Subject.StartsWith("[FIRING:"))
            {
                // avoids cycle if alert comes from Alertmanager (even not originating from LocalSmtpRelay).
                return true;
            }

            return false;
        }

        private static void SetAlertProperties(Alert alert, MessageRule rule)
        {
            if (!string.IsNullOrEmpty(rule.AlertName))
            {
                alert.SetName(rule.AlertName);
            }
            if (rule.GeneratorUrl != null)
            {
                alert.GeneratorURL = rule.GeneratorUrl;
            }

            SetStrings(alert.Labels, rule.Labels);
            SetStrings(alert.Annotations, rule.Annotations);
        }

        private static void SetStrings(Dictionary<string, string> destination, string[] values)
        {
            foreach (var item in values)
            {
                var parts = item.Split(':', 2);
                if (parts.Length == 2)
                {
                    destination[parts[0]] = parts[1];
                }
            }
        }

        private async Task<bool> IsAlertManagerReady(CancellationToken cancellationToken)
        {
            if (_amStatusOk)
                return true;

            var status = await alertManagerClient.GetStatus(cancellationToken);
            if (status != null)
            {
                if (status.IsReady())
                {
                    _amStatusOk = true;
                    logger.LogInformation("Alertmanager is ready: {Status}", JsonSerializer.Serialize(status));
                }
                else
                {
                    logger.LogWarning("Alertmanager is not ready: {Status}", JsonSerializer.Serialize(status));
                }
            }
            return _amStatusOk;
        }
    }
}
