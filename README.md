![Kubernetes](https://img.shields.io/badge/kubernetes-%23326ce5.svg?style=for-the-badge&logo=kubernetes&logoColor=white) ![Docker](https://img.shields.io/badge/docker-%230db7ed.svg?style=for-the-badge&logo=docker&logoColor=white)

# LocalSmtpRelay

*LocalSmtpRelay*'s first purpose is to act as a simple local SMTP relay to another remote SMTP.

It has been made to easily provide access to an SMTP server for services running on (trusted) local network (LAN) without having to give real account password to these services. For example, if you want to use Gmail's SMTP, you would need to give access to your account to any program requiring your SMTP settings. This access allows not only to send emails but also to read them (with proper IMAP or POP3 settings). The idea of this program is to set your actual account password only in one place (this SMTP relay), and to make other services to send emails through this SMTP relay.

Internally, [MailKit](https://github.com/jstedfast/MailKit) is used for SMTP forwarding (client), [SmtpServer](https://github.com/cosullivan/SmtpServer) is used for local SMTP (server).

## Additional features

- Forward some messages to [Prometheus Alertmanager](https://github.com/prometheus/alertmanager). This allows to interface systems with Alertmanager even if they do not support it out of the box, as long as they support SMTP notification messages.
- Replace subject of some messages with LLM completion based on message body (OpenAI-compatible API like [llama.cpp](https://github.com/ggerganov/llama.cpp/tree/master)).
- Filter by destination address (if enabled, SMTP will reject unknown recipients).
- Filter by message size (SMTP will reject message reaching a threshold).
- Catch up on next program startup (messages not send yet at shutdown are sent on startup).
- Unsecure Basic authentication (user/password without TLS) and anonymous authentication.

## Limitations

Current known limitations:

- No secure connection for inbound messages. TLS/SSL is supported on destination SMTP / outbound messages.

## Deployment

You can:

- Compile and run binaries (with .NET SDK).
- Run it with [Docker](examples/docker/) (see folder examples).
- Run it with [Kubernetes](examples/kubernetes/) (see folder examples).

## Configuration

See [Configuration](examples/configuration/README.md) in folder examples.

## Usage

Once running, you can send email through this relay:

Example from [MailKit](https://github.com/jstedfast/MailKit):

```csharp
using System;

using MailKit.Net.Smtp;
using MailKit;
using MimeKit;

namespace TestClient {
 class Program
 {
  public static void Main (string[] args)
  {
   var message = new MimeMessage ();
   message.From.Add (new MailboxAddress ("Joey Tribbiani", "joey@friends.com"));
   message.To.Add (new MailboxAddress ("Mrs. Chanandler Bong", "chandler@friends.com"));
   message.Subject = "How you doin'?";

   message.Body = new TextPart ("plain") {
    Text = @"Hey Chandler,

I just wanted to let you know that Monica and I were going to go play some paintball, you in?

-- Joey"
   };

   using (var client = new SmtpClient ()) {
    client.Connect("localhost", 25);
    client.Authenticate("test", "test");
    client.Send (message);
    client.Disconnect (true);
   }
  }
 }
}
```
