# LocalSmtpRelay

*LocalSmtpRelay* purpose is to act as a simple SMTP relay to another SMTP.

It has been made to easily provide access to an SMTP server for services running on (trusted) local network (LAN) without having to give real account password to these services. For example, if you want to use Gmail's SMTP, you would need to give access to your account to any program requiring your SMTP settings. This access allows not only to send emails but also to read them (with proper IMAP or POP3 settings). The idea of this program is to set your actual account password only in one place (this SMTP relay), and to make other services to send emails through this SMTP relay.

There certainly exists lots of programs of this kind, but I thought this was a fun hobby project.

Internally, [MailKit](https://github.com/jstedfast/MailKit) is used for SMTP forwarding (client), [SmtpServer](https://github.com/cosullivan/SmtpServer) is used for local SMTP (server).

## Usage

See settings in *appsettings.json*, they are mostly self explanatory (they also are detailed later in this readme).

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

### Throttling

Because this relay is made for occasional email notifications on a small LAN (typically at home), there is an aggressive throttling mechanism: only *n* messages can be processed at a time (there is a queue). Once queue is full, new messages are rejected with SMTP status "service unavailable (421)" sent to client. Queue size (*n*) can be configured in *appsettings.json*. Rejected messages can be deleted automatically or not (see settings).

### Filter by destination address

Settings allow to filter destination addresses. If a message is received with an unknown destination address, it will be rejected with SMTP status "mailbox unavailable (550)". Note rejected messages in this case are not even stored on file system (SMTP transaction rejected before receiving message body).

To disable this filter, remove any value in app setting "DestinationAddressWhitelist".

### Filter by message size

Similar to filter by destination address: if message size exceeds "MaxMessageLengthBytes", it will be rejected with "size limit exceeded (552)".

### Shutdown

If program is shutdown while messages are in queue, they are not sent. At next startup, stored messages will be enqueued (can be disabled through app settings). Be aware there is no rejection throttling at this stage. If previously rejected messages are not automatically deleted, they will be sent in this startup phase.

## Limitations

Current known limitations:

- No secure connection for inbound messages. TLS/SSL is supported on destination SMTP / outbound messages.

## Implementation

LocalSmtpRelay is composed of three main components

* SmtpServerBackgroundService: listen to inbound messages (local SMTP server).
* MessageStore: stores messages on file system until they are sent (or rejected).
* SmtpForwarder: forward messages to actual SMTP of your choice and delete message from file system.

## Deployement with Docker

There are many ways to deploy this solution with Docker: docker alone, docker-compose, or docker swarm. Here I describe deployment with docker swarm.

### Pre-requisite

- Docker and Docker Swarm

While Docker Swarm is not strictly required, I recommend it for support of [docker secrets](https://docs.docker.com/engine/swarm/secrets/). This avoids to store your SMTP password in clear on your docker host. If you have docker running without Swarm, just run `docker swarm init` ([full documentation here](https://docs.docker.com/engine/swarm/swarm-tutorial/create-swarm/)). You can also check if your docker host is already in Swarm mode with `docker info`.

### Setup your SMTP password

This command requires Docker Swarm.

``` shell
printf "your password" | docker secret create smtp_password -
```

### Clone repository and setup docker-compose

Strictly speaking, you do not need to clone this entire repository. You'll just need (from directory */src/LocalSmtp*):

- docker-compose-example.yml
- /var/appsettings.json

**So you can clone this repository or just download aforementioned files.**


- Update `var/appsettings.json` as needed. Note `docker-compose-example.yml` overrides `SmtpForwarder__Authentication__PasswordFile` so you don't have to store your SMTP password in clear, and you can ignore this setting in `appsettings.json` file.
- Copy `docker-compose-example.yml` to `docker-compose.yml`
- Update `docker-compose.yml` as needed:
  - By default, example file assumes container will join an overlay network named `my-attachable-overlay` (*overlay* network and not *bridge* network to be compatible with docker swarm). You can adapt this (or you can [create this network](https://docs.docker.com/network/overlay/) with command `docker network create -d overlay --attachable my-attachable-overlay`).

If you prefer not to use *docker secrets*, you can set your SMTP password in clear in `docker-compose.yml` by replacing `SmtpForwarder__Authentication__PasswordFile` by `SmtpForwarder__Authentication__Password` (value is password in clear). Alternatively you can set password directly in `appsettings.json`.

### Run it

From directory containing `docker-compose.yml`:

```shell
docker stack deploy -c docker-compose.yml localsmtprelay
```

If you need to stop it:

```shell
docker stack rm localsmtprelay
```

Configure your local SMTP clients:

- From another container on same docker host: use hostname `localsmtprelay` and port 25.
- From docker host: use `localhost` and port binding set in `docker-compose.yml` (default: port 25).
- From another machine: use docker host IP address.

### Update version

You don't need to stop current deployment.

- Update file `docker-compose.yml` to reflect new version of `image` field.
- Deploy or update with `docker stack deploy -c docker-compose.yml localsmtprelay`

## Configuration

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

