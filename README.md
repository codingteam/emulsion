<!--
SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>

SPDX-License-Identifier: MIT
-->

emulsion [![Docker Image][badge.docker]][docker-hub] [![Status Aquana][status-aquana]][andivionian-status-classifier]
========

emulsion is a bridge between [Telegram][telegram] and [XMPP][xmpp].

Installation
------------
There are two supported Emulsion distributions: as a framework-dependent .NET application, or as a Docker image.

### .NET Application
To run Emulsion as [a framework-dependent .NET application][docs.dotnet.framework-dependent], you'll need to [install .NET runtime][dotnet] version 8.0 or later.

Then, download the required version in the [Releases][releases] section.

After that, configure the application (see the following section), and start it using the following shell command:

```console
$ dotnet Emulsion.dll [optional-path-to-json-config-file]
```

If `optional-path-to-json-config-file` is not provided, Emulsion will use the `emulsion.json` file from the current directory.

### Docker
It is recommended to use Docker to deploy this application. To install the application from Docker, you may use the following Bash script:

```bash
NAME=emulsion
EMULSION_VERSION=latest
CONFIG=/opt/codingteam/emulsion/emulsion.json
DATA=/opt/codingteam/emulsion/data # optional
WEB_PORT=5051 # optional
docker pull codingteam/emulsion:$EMULSION_VERSION
docker rm -f $NAME
docker run --name $NAME \
    -v $CONFIG:/app/emulsion.json:ro \
    -v $DATA:/data \
    -p 127.0.0.1:$WEB_PORT:5000 \
    --restart unless-stopped \
    -d \
    codingteam/emulsion:$EMULSION_VERSION
```

where

- `$NAME` is the container name
- `$EMULSION_VERSION` is the image version you want to deploy, or `latest` for
  the latest available one
- `$CONFIG` is the **absolute** path to the configuration file
- `$DATA` is the absolute path to the data directory (used by the configuration)
- `$WEB_PORT` is the port on the host system which will be used to access the content proxy

Configuration
-------------
Copy `emulsion.example.json` to `emulsion.json` and set the settings. For some settings, there are defaults:

```json
{
    "xmpp": {
        "roomPassword": null,
        "connectionTimeout": "00:05:00",
        "messageTimeout": "00:05:00",
        "pingInterval": null,
        "pingTimeout": "00:00:30"
    },
    "fileCache": {
        "fileSizeLimitBytes": 1048576,
        "totalCacheSizeLimitBytes": 20971520
    },
    "messageArchive": {
        "isEnabled": false
    }
}
```

All the other settings are required, except the `database`, `hosting` and `fileCache` sections (the corresponding functionality will be turned off if the sections aren't filled).

Note that `pingInterval` of `null` disables XMPP ping support.

`telegram.messageThreadId` allows to connect the bot to a particular message thread: any messages from the other threads will be ignored, and the bot will send its messages to the selected thread only.

`messageArchive.isEnabled` will enable or disable the message archive functionality. If enabled, the bot will save all the incoming messages to the database (so, `database` section from the next section is required for that to work).

### Telegram Content Proxy and Web Service

There's Telegram content proxy support, for XMPP users to access Telegram content without directly opening links on t.me.

To enable it, configure the `database`, `hosting` and `fileCache` configuration file sections:

```json
{
    "database": {
        "dataSource": "sqliteDatabase.db"
    },
    "hosting": {
        "externalUriBase": "https://example.com/api/",
        "bindUri": "http://*:5000/",
        "hashIdSalt": "test"
    },
    "fileCache": {
        "directory": "/tmp/emulsion/cache",
        "fileSizeLimitBytes": 1048576,
        "totalCacheSizeLimitBytes": 20971520
    }
}
```

`dataSource` may be a path to the SQLite database file on disk. If set, Emulsion will automatically apply necessary migrations to this database on startup.

If all the parameters are set, then Emulsion will save the incoming messages into the database, and will then insert links to `{externalUriBase}/content/{contentId}` instead of links to `https://t.me/{messageId}`.

`bindUri` designates the URI the web server will listen locally (which may or may not be the same as the `externalUriBase`).

The content identifiers in question are generated from the database ones using the [hashids.net][hashids.net] library, `hashIdSalt` is used in generation. This should complicate guessing of content ids for any external party not reading the chat directly.

If the `fileCache.directory` option is not set, then the content proxy will only generate redirects to corresponding t.me URIs. Otherwise, it will store the downloaded files (that fit the cache) in a cache on disk; the items not fitting into the cache will be proxied to clients.

### Recommended Network Configuration

Current configuration system allows the following:

1. Set up a reverse proxy for, say, `https://example.com/telegram` taking the content from `http://localhost/`.
2. When receiving a piece of Telegram content (a file, a photo, an audio message), the bot will send a link to `https://example.com/telegram/content/<some_id>` to the XMPP chat.
3. When anyone visits the link, the reverse proxy will send a request to `http://localhost/content/<some_id>`, which will take a corresponding content from the database.

Documentation
-------------
- [Changelog][docs.changelog]
- [Contributor Guide][docs.contributing]
- [Maintainership][docs.maintainership]

License
-------
The project is distributed under the terms of [the MIT license][docs.license].

The license indication in the project's sources is compliant with the [REUSE specification v3.3][reuse.spec].

[andivionian-status-classifier]: https://github.com/ForNeVeR/andivionian-status-classifier#status-aquana-
[badge.docker]: https://img.shields.io/docker/v/codingteam/emulsion?sort=semver
[docker-hub]: https://hub.docker.com/r/codingteam/emulsion
[docs.changelog]: ./CHANGELOG.md
[docs.contributing]: CONTRIBUTING.md
[docs.dotnet.framework-dependent]: https://learn.microsoft.com/en-us/dotnet/core/deploying/#publish-framework-dependent
[docs.license]: ./LICENSE.md
[docs.maintainership]: MAINTAINERSHIP.md
[dotnet]: https://dot.net/
[hashids.net]: https://github.com/ullmark/hashids.net
[releases]: https://github.com/codingteam/emulsion/releases
[reuse.spec]: https://reuse.software/spec-3.3/
[status-aquana]: https://img.shields.io/badge/status-aquana-yellowgreen.svg
[telegram]: https://telegram.org/
[xmpp]: https://xmpp.org/
