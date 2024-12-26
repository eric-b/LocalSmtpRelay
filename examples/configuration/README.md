# Configuration

Main settings in `appsettings.json` are:

- `SendMessageAtStartup`: Configures notification email sent at startup.
  - `Subject`: email subject (if not set, no email is sent at startup)
  - `To`: email address (if not set, no email is sent at startup).
  - `From`: email address (optional).
  - `Body`: body (optional).
- `MessageStore`:
  - `Directory`: temporary directory path where messages are stored until sent.
  - `DeleteRejectedMessages`: `true`/`false` - if `true`, rejected messages are deleted from temporary directory.
  - `CatchUpOnStartup`: `true`/`false` - if `true`, messages present in temporary directory when program starts are sent.
  - `RejectEmptyRecipient`: `true`/`false` - if `true`, messages without a value in field `To` are rejected. If `false` and if `SmtpForwarder.DefaultRecipient` is set, default recipient will be used to send message.
  - `DestinationAddressWhitelist`: array of email addresses allowed as recipient. If empty, this check is disabled. If not empty, any message with an unknown recipient will be rejected.
- `SmtpServer`: Configures local SMTP server settings for receiving messages.
  - `Hostname`: host (should be `localhost`)
  - `Ports`: array of ports to bind (default: `25`).
  - `Accounts`: array of accounts (if empty, anonymous clients will be allowed)
    - `Username`: username
    - `PasswordFile`: path of file containing password (if not set, `Password` will be read).
    - `Password`: password.
  - `AllowAnonymous`: `true` or `false`. If `true`, any client can send email without any authentication, or if authentication command is sent, wrong user/passwords are allowed.
- `SmtpForwarder`: Configures SMTP client settings for sending messages.
  - `DefaultRecipient`: should be your email address.
  - `Disable`: `true`/`false` - if `true`, no message will be sent until this flag is set to `false` (or removed).
  - `MaxQueueLength`: number of allowed messages in send queue (throttling).
  - `Hostname`: SMTP host
  - `Port`: SMTP port
  - `EnableSsl`: `true`/`false`
  - `Authentication`: SMTP authentication
    - `Username`: username
    - `PasswordFile`: path of file containing password (if not set, `Password` will be read).
    - `Password`: password

## Alertmanager API

To interface with Prometheus Alertmanager API, you'll need to provide API base URL and to setup interception rules:

```js  
{
    "AlertManagerForwarder": {
        "BaseUrl": "http://alertmanager-service:9093/",
        "MessageRules": [
            {
                "AlertName": "pfsense-routing-gateway",
                "RegexOnField": "Body",
                "AlertRegex": "omitting from routing group",
                "ResolutionRegex": "adding to routing group",
                "ResolutionTimeout": "06:00:00",
                "Labels": [ "severity:warning" ],
                "Annotations": [ "summary:Routing gateway has packet loss" ],
                "GeneratorUrl": "http://pfsense/",
                "RunLlmOnBody": false
            }
        ]
    }
}
```

The previous example will intercept notifications from pfSense. Some contain "omitting from routing group" (*AlertRegex*), that will trigger an alert named "pfsense-routing-gateway". Some others will mark the alert resolved because they contain "adding to routing group" (*ResolutionRegex*). Unless resolved, an alert stay active for 6 hours (*ResolutionTimeout*) since the last notification.

By default, an annotation "description" is added with body of the notification. Parameter *RunLlmOnBody* allows to infer this description from an OpenAi-compatible API.

## OpenAi-compatible API (LLM)

An LLM API can be used for:

- Inferring a better annotation "description" in alerts sent to Alertmanager.
- Inferring a better message subject for some messages, typically useful when SMTP client is too generic and does not provide meaningful information about the body of the message.

The API can be served by the most well known OpenAi ChatGPT, or any other compatible server like [llama.cpp](https://github.com/ggerganov/llama.cpp/tree/master) (that can be run locally).

Primary configuration is:

```js 
{
    "LlmClient": {
        "BaseUrl": "http://llama-service:8080/",
        "ApiKey": "",
        "DisableLlmHealthCheck": false,
        "RequestTimeout": "00:05:00",
        "Parameters": {
            "Temperature": 0.7,
            "Model": "OpenAiModel",
            "SystemPrompt": ""
        }
    }
}
```

For true OpenAi API, you'll need to set your *ApiKey* and to disable healthcheck (*DisableLlmHealthCheck*) because this one depends on the endpoint provided by Llama.cpp server and not by OpenAi API.

All parameters in section *Parameters* are optional. There also exists a parameter `TopP` (a float value) not described in the previous example. If *Temperature* or *TopP* are not set, no value is sent and server will rely on its defaults. The *SystemPrompt* corresponds to the instructions sent with the "system" (or "developer") role. LocalSmtpRelay has a hard-coded default value if not set.

### LLM with Alerts

To use LLM with alerts forwarded to Prometheus Alertmanager, use parameter `RunLlmOnBody=true` (see configuration example for *AlertManagerForwarder* in this page).

### LLM with other messages

To use LLM with other messages sent by *SmtpForwarder*, define rules in the sub section *LlmEnrichment* like this:

```js 
{
    "SmtpForwarder": {
        "LlmEnrichment": {
            "Rules": [
                {
                    "RegexOnField": "Subject",
                    "Regex": "notification",
                    "SubjectPrefix": "notif: "
                }
            ]
        }
    }
}
```

The previous example will replace subject of messages that contain "notification" in their original subject. The parameter *SubjectPrefix* is a prefix to the subject suggested by the LLM.

The prompt sent to LLM will contain default user instructions followed by message body. You can customize the user prompt with a parameter *UserPrompt* inside a rule (it must contain a placeholder `%1` for replacement with message body). You also can customize system prompt with a parameter *SystemPrompt* in section *LlmEnrichment* (no variable placeholder required for the system prompt), or directly in primary configuration of *LlmClient* (see related section in this page).
Unless for complex configuration, custom system prompt should be in *LlmClient* (shared for any usage of the LLM client).
