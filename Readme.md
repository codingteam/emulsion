emulsion [![Appveyor Build][badge-appveyor]][build-appveyor] [![Travis Build][badge-travis]][build-travis] [![Status Umbra][status-umbra]][andivionian-status-classifier]
========

emulsion is a bridge between [Telegram][telegram] and [XMPP][xmpp].

Build
-----

Install [.NET Core SDK][dotnet-core-sdk] for your platform, then run:

```console
$ dotnet build
```

Configure
---------

Copy `emulsion.example.json` to `emulsion.json` and set the settings.

Test
----

```console
$ dotnet test ./Emulsion.Tests
```

Run
---

Requires [.NET Core Runtime][dotnet-core-runtime] version 2.0+.

```console
$ dotnet run --project ./Emulsion [optional-path-to-json-config-file]
```

[andivionian-status-classifier]: https://github.com/ForNeVeR/andivionian-status-classifier#status-umbra-
[build-appveyor]: https://ci.appveyor.com/project/ForNeVeR/emulsion/branch/master
[build-travis]: https://travis-ci.org/codingteam/emulsion
[dotnet-core-runtime]: https://www.microsoft.com/net/download/core#/runtime
[dotnet-core-sdk]: https://www.microsoft.com/net/download/core
[telegram]: https://telegram.org/
[xmpp]: https://xmpp.org/

[badge-appveyor]: https://ci.appveyor.com/api/projects/status/dgrpxj0dx221ii89/branch/master?svg=true
[badge-travis]: https://travis-ci.org/codingteam/emulsion.svg?branch=master
[status-umbra]: https://img.shields.io/badge/status-umbra-red.svg
