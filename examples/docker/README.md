# Deployement with Docker

There are many ways to deploy this solution with Docker: docker alone, docker-compose, or docker swarm. Here I describe deployment with docker swarm.

## Pre-requisite

- Docker and Docker Swarm

While Docker Swarm is not strictly required, I recommend it for support of [docker secrets](https://docs.docker.com/engine/swarm/secrets/). This avoids to store your SMTP password in clear on your docker host. If you have docker running without Swarm, just run `docker swarm init` ([full documentation here](https://docs.docker.com/engine/swarm/swarm-tutorial/create-swarm/)). You can also check if your docker host is already in Swarm mode with `docker info`.

## Setup your SMTP password

This command requires Docker Swarm.

``` shell
printf "your password" | docker secret create smtp_password -
```

## Clone repository and setup docker-compose

Strictly speaking, you do not need to clone this entire repository. You'll just need:

- /src/docker-compose-example.yml
- /var/appsettings.json

**So you can clone this repository or just download aforementioned files.**


- Update `var/appsettings.json` as needed. Note `docker-compose-example.yml` overrides `SmtpForwarder__Authentication__PasswordFile` so you don't have to store your SMTP password in clear, and you can ignore this setting in `appsettings.json` file.
- Copy `docker-compose-example.yml` to `docker-compose.yml`
- Update `docker-compose.yml` as needed:
  - By default, example file assumes container will join an overlay network named `my-attachable-overlay` (*overlay* network and not *bridge* network to be compatible with docker swarm). You can adapt this (or you can [create this network](https://docs.docker.com/network/overlay/) with command `docker network create -d overlay --attachable my-attachable-overlay`).

If you prefer not to use *docker secrets*, you can set your SMTP password in clear in `docker-compose.yml` by replacing `SmtpForwarder__Authentication__PasswordFile` by `SmtpForwarder__Authentication__Password` (value is password in clear). Alternatively you can set password directly in `appsettings.json`.

## Run it

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

## Update version

You don't need to stop current deployment.

- Update file `docker-compose.yml` to reflect new version of `image` field.
- Deploy or update with `docker stack deploy -c docker-compose.yml localsmtprelay`