emulsion [![Docker Image][badge.docker]][docker-hub] [![Status Aquana][status-aquana]][andivionian-status-classifier]
========

emulsion is a bridge between [Telegram][telegram] and [XMPP][xmpp].

Build
-----

Install [.NET Core SDK][dotnet-core-sdk] 3.1 or newer for your platform, then
run:

```console
$ dotnet build
```

Configure
---------

Copy `emulsion.example.json` to `emulsion.json` and set the settings. For some
settings, there're defaults:

```json
{
    "xmpp": {
        "roomPassword": null,
        "messageTimeout": "00:05:00",
        "pingInterval": null,
        "pingTimeout": "00:00:30"
    }
}
```

All the other settings are required.

Note that `pingInterval` of `null` disables XMPP ping support.

Test
----

To execute the tests:

```console
$ dotnet test
```

Run
---

Requires [.NET Core Runtime][dotnet-core-runtime] version 3.1 or newer.

```console
$ dotnet run --project ./Emulsion [optional-path-to-json-config-file]
```

Docker
------
It is recommended to use Docker to deploy this project. To install the
application from Docker, you may use the following Bash script:

```bash
NAME=emulsion
EMULSION_VERSION=latest
CONFIG=/opt/codingteam/emulsion/emulsion.json
docker pull codingteam/emulsion:$EMULSION_VERSION
docker rm -f $NAME
docker run --name $NAME \
    -v $CONFIG:/app/emulsion.json:ro \
    --restart unless-stopped \
    -d \
    codingteam/emulsion:$EMULSION_VERSION
```

where

- `$NAME` is the container name
- `$EMULSION_VERSION` is the image version you want to deploy, or `latest` for
  the latest available one
- `$CONFIG` is the **absolute** path to the configuration file

To build and push the container to Docker Hub, use the following commands:

```console
$ docker build -t codingteam/emulsion:$EMULSION_VERSION \
    -t codingteam/emulsion:latest .

$ docker login # if necessary
$ docker push codingteam/emulsion:$EMULSION_VERSION
$ docker push codingteam/emulsion:latest
```

where `$EMULSION_VERSION` is the version of the image to publish.

Documentation
-------------

- [Changelog][changelog]
- [License (MIT)][license]

[andivionian-status-classifier]: https://github.com/ForNeVeR/andivionian-status-classifier#status-aquana-
[changelog]: ./CHANGELOG.md
[docker-hub]: https://hub.docker.com/r/codingteam/emulsion
[dotnet-core-runtime]: https://www.microsoft.com/net/download/core#/runtime
[dotnet-core-sdk]: https://www.microsoft.com/net/download/core
[license]: ./LICENSE.md
[telegram]: https://telegram.org/
[xmpp]: https://xmpp.org/

[badge.docker]: https://img.shields.io/docker/v/codingteam/emulsion?sort=semver
[status-aquana]: https://img.shields.io/badge/status-aquana-yellowgreen.svg
