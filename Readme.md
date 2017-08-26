emulsion
========

emulsion is a bridge between [Telegram][telegram] and [XMPP][xmpp].

Build
-----

Install [.NET Core SDK][dotnet-core-sdk] for your platform, then run:

```
$ dotnet restore
$ dotnet build
```

Configure
---------

Copy `emulsion.example.json` to `emulsion.json` and set the settings.

Run
---

Requires [.NET Core Runtime][dotnet-core-runtime] version 1.1+.

```
$ dotnet run [optional-path-to-json-config-file]
```

[dotnet-core-runtime]: https://www.microsoft.com/net/download/core#/runtime
[dotnet-core-sdk]: https://www.microsoft.com/net/download/core
[telegram]: https://telegram.org/
[xmpp]: https://xmpp.org/
