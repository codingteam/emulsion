emulsion [![Appveyor Build][badge-appveyor]][build-appveyor] [![Travis Build][badge-travis]][build-travis] [![Status Enfer][status-enfer]][andivionian-status-classifier]
========

emulsion is a bridge between [Telegram][telegram] and [XMPP][xmpp].

Build
-----

Install [.NET Core SDK][dotnet-core-sdk] 3.0 or newer for your platform, then
run:

```console
$ dotnet build
```

Configure
---------

Copy `emulsion.example.json` to `emulsion.json` and set the settings.

Test
----

To execute the tests:

```console
$ dotnet test ./Emulsion.Tests
```

Run
---

Requires [.NET Core Runtime][dotnet-core-runtime] version 2.2+.

```console
$ dotnet run --project ./Emulsion [optional-path-to-json-config-file]
```

Docker Compose
--------------

To deploy the application using the provided Docker Compose configuration file,
execute this:

```console
$ docker-compose --project-name emulsion up --build --force-recreate -d
```

Docker
------

We also have a Dockerfile. To deploy it, first author a configuration file, and
then:

```console
$ docker build -t emulsion .
$ docker run -d --name emulsion -v $PWD/emulsion.json:/app/emulsion.json:ro emulsion
```

[andivionian-status-classifier]: https://github.com/ForNeVeR/andivionian-status-classifier#status-enfer-
[build-appveyor]: https://ci.appveyor.com/project/ForNeVeR/emulsion/branch/master
[build-travis]: https://travis-ci.org/codingteam/emulsion
[dotnet-core-runtime]: https://www.microsoft.com/net/download/core#/runtime
[dotnet-core-sdk]: https://www.microsoft.com/net/download/core
[telegram]: https://telegram.org/
[xmpp]: https://xmpp.org/

[badge-appveyor]: https://ci.appveyor.com/api/projects/status/dgrpxj0dx221ii89/branch/master?svg=true
[badge-travis]: https://travis-ci.org/codingteam/emulsion.svg?branch=master
[status-enfer]: https://img.shields.io/badge/status-enfer-orange.svg
