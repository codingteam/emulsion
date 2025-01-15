<!--
SPDX-FileCopyrightText: 2025 Emulsion contributors <https://github.com/codingteam/emulsion>

SPDX-License-Identifier: MIT
-->

# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic
Versioning v2.0.0](https://semver.org/spec/v2.0.0.html).

When considering the public API, we take into account the tool configuration and external requirements of the framework-dependent binary. Meaning that basically, breaking changes in configuration files, command-line syntax, or in the runtime requirements should be causing a major version increment.

## [4.0.0] - 2025-01-15
### Changed
- **(Requirement update!)** Update to .NET 9.
- Update all the used libraries.

### Fixed
- [#322: Broken Unicode message destroys the XMPP connection](https://github.com/codingteam/emulsion/issues/322).

## [3.0.0] - 2024-06-23
### Changed
- **(Requirement update!)** Update to .NET 8.
- Update all the used libraries.
- Migrate the internal architecture away from using Akka.NET to channels.

### Added
- [#188: Telegram: support new quotes](https://github.com/codingteam/emulsion/issues/188).
- Release artifacts as a framework-dependent .NET application (not exclusively a Docker image now).

### Fixed
- The second part of [#190](https://github.com/codingteam/emulsion/issues/190): now the Telegram errors will be logged to the log file, not to the stdout.

## [2.4.4] - 2024-02-06
### Changed
- Downgrade to .NET 7 (the hosting infrastructure turns out to be unable to handle a newer one so far).

## [2.4.3] - 2024-02-06
### Changed
- **(Requirement update!)** Update to .NET 8.

### Fixed
- [#190](https://github.com/codingteam/emulsion/issues/190): a quotation message caused the bot to fail the parsing.

## [2.4.2] - 2023-07-07
### Fixed
- Message archive entries are stored by id in a stable way
### Added
- Message archive: it's now possible to select the message amount shown per page.

## [2.4.1] - 2023-07-06
### Fixed
- Restore old content URL on `/content` instead of new `/api/content`
- Fix the frontend content search procedure for the message archive
- Add the frontend content to the Docker image

## [2.4.0] - 2023-07-06
### Changed
- Runtime: upgrade to .NET 7
- Language: upgrade to F# 7
### Added
- Optional message archive available through the web interface ([#138](https://github.com/codingteam/emulsion/issues/138))

## [2.3.1] - 2023-02-04
### Fixed
- [#175: Error when passing a relative path to the config file](https://github.com/codingteam/emulsion/issues/175)
- [#169: Possibly incorrect content type specification for animations](https://github.com/codingteam/emulsion/issues/169)

### Changed
- Upgrade Akka to 1.4.46
- Portable debug type is now used instead of full

## [2.3.0] - 2022-11-15
### Added
- A new configuration parameter, `telegram.messageThreadId`, to support new message thread feature of Telegram.

## [2.2.0] - 2022-08-28
### Added
- [#165: Telegram content proxy: inline images and other supported data types](https://github.com/codingteam/emulsion/issues/165)
- Telegram: generate links for content in private chats

## [2.1.0] - 2022-08-28
### Added
- Finished file cache and content proxy support for Telegram

## [2.0.2] - 2022-07-03
### Added
- Full syntax of `ASPNETCORE_URLS`, including wildcards, now may be used in `hosting.bindUri`

## [2.0.1] - 2022-07-03
### Fixed
- Docker image now uses `mcr.microsoft.com/dotnet/aspnet:6.0` (because the new features require ASP.NET Core runtime)

## [2.0.0] - 2022-07-03
### Changed
- `hosting.baseUri` configuration parameter is now `hosting.externalUriBase`
- Photos are now stored in the database without thumbnail duplicates (only full versions)

### Added
- [#147: Telegram content redirector support](https://github.com/codingteam/emulsion/issues/147). Emulsion is now able to generate redirects to t.me from its own embedded web server, knowing the internal content id.

  This feature is not very useful, yet (earlier, the same t.me links were already available to the users directly), but it is the first step for further development of more valuable forms of content proxying.

## [1.9.0] - 2022-06-01
### Changed
- Runtime: upgrade to .NET 6

### Added
- Preliminary database support (disabled by default): Emulsion is now able to store information about Telegram content in an SQLite database

## [1.8.0] - 2021-09-26
### Changed
- Additional logging for mailbox processor errors

### Added
- XMPP: connection timeout support ([#141](https://github.com/codingteam/emulsion/issues/141))

## [1.7.0] - 2021-06-29
### Changed
- Runtime: upgrade to .NET 5
- XMPP: improve the ping activity stability (get rid of `Async.Start` calls)

### Fixed
- Telegram: [issue #133](https://github.com/codingteam/emulsion/issues/133) caused by improper error processing

## [1.6.1] - 2020-12-30
### Changed
- XMPP: add an empty line after the quote part of a Telegram message

## [1.6.0] - 2020-11-16
### Added
- Telegram: support messages forwarded from hidden users and channels

## [1.5.0] - 2020-11-01
### Fixed
- [XMPP: errors on message receival aren't being recognized
  (#105)](https://github.com/codingteam/emulsion/issues/105)
- [Telegram: "\[DATA UNRECOGNIZED\]" on every message in a private room
  (#115)](https://github.com/codingteam/emulsion/issues/115)

### Added
- Telegram: translate animations as links
- Telegram: translate unknown media types as links
- [Telegram: disable limits for forwarded messages
  (#120)](https://github.com/codingteam/emulsion/issues/120)
- [XMPP: send ping messages periodically to check the connection
  (#88)](https://github.com/codingteam/emulsion/issues/88)
- [XMPP: Support timeouts in XMPP message receival
  (#101)](https://github.com/codingteam/emulsion/issues/101)
- XMPP: support password-protected rooms

### Changed
- Project is now published as a Docker image

## [1.4.0] - 2020-06-25
### Changed
- Project migrated to .NET Core 3.1

### Added
- Telegram: support replies to user join messages
- Telegram: translate stickers as links

## [1.3.0] - 2020-04-05
### Added
- Telegram: translate photos as links

### Changed
- Telegram: user enter and leave message improvements

## [1.2.0] - 2020-02-24
### Added
- Telegram: poll support
- Telegram: user enter and leave message support

## [1.1.1] - 2020-02-23
### Fixed
- XMPP: an issue when trying to parse a user nickname with `@` in it

## [1.1.0] - 2020-02-15
### Changed
- Project migrated to .NET Core 3.0
### Added
- Telegram: image caption support

## [1.0.1] - 2019-10-30
### Fixed
- [Telegram: private messages are being sent to an XMPP
  conference (#85)](https://github.com/codingteam/emulsion/issues/85)

## [1.0.0] - 2019-10-17
This is the first versioned release of the project. It is a message bridge
between an XMPP conference and a Telegram room. The project runs on .NET Core
runtime 2.2.

[1.0.0]: https://github.com/codingteam/emulsion/releases/tag/v1.0.0
[1.0.1]: https://github.com/codingteam/emulsion/compare/v1.0.0...v1.0.1
[1.1.0]: https://github.com/codingteam/emulsion/compare/v1.0.1...v1.1.0
[1.1.1]: https://github.com/codingteam/emulsion/compare/v1.1.0...v1.1.1
[1.2.0]: https://github.com/codingteam/emulsion/compare/v1.1.1...v1.2.0
[1.3.0]: https://github.com/codingteam/emulsion/compare/v1.2.0...v1.3.0
[1.4.0]: https://github.com/codingteam/emulsion/compare/v1.3.0...v1.4.0
[1.5.0]: https://github.com/codingteam/emulsion/compare/v1.4.0...v1.5.0
[1.6.0]: https://github.com/codingteam/emulsion/compare/v1.5.0...v1.6.0
[1.6.1]: https://github.com/codingteam/emulsion/compare/v1.6.0...v1.6.1
[1.7.0]: https://github.com/codingteam/emulsion/compare/v1.6.1...v1.7.0
[1.8.0]: https://github.com/codingteam/emulsion/compare/v1.7.0...v1.8.0
[1.9.0]: https://github.com/codingteam/emulsion/compare/v1.8.0...v1.9.0
[2.0.0]: https://github.com/codingteam/emulsion/compare/v1.9.0...v2.0.0
[2.0.1]: https://github.com/codingteam/emulsion/compare/v2.0.0...v2.0.1
[2.0.2]: https://github.com/codingteam/emulsion/compare/v2.0.1...v2.0.2
[2.1.0]: https://github.com/codingteam/emulsion/compare/v2.0.2...v2.1.0
[2.2.0]: https://github.com/codingteam/emulsion/compare/v2.1.0...v2.2.0
[2.3.0]: https://github.com/codingteam/emulsion/compare/v2.2.0...v2.3.0
[2.3.1]: https://github.com/codingteam/emulsion/compare/v2.3.0...v2.3.1
[2.4.0]: https://github.com/codingteam/emulsion/compare/v2.3.1...v2.4.0
[2.4.1]: https://github.com/codingteam/emulsion/compare/v2.4.0...v2.4.1
[2.4.2]: https://github.com/codingteam/emulsion/compare/v2.4.1...v2.4.2
[2.4.3]: https://github.com/codingteam/emulsion/compare/v2.4.2...v2.4.3
[2.4.4]: https://github.com/codingteam/emulsion/compare/v2.4.3...v2.4.4
[3.0.0]: https://github.com/codingteam/emulsion/compare/v2.4.4...v3.0.0
[4.0.0]: https://github.com/codingteam/emulsion/compare/v3.0.0...v4.0.0
[Unreleased]: https://github.com/codingteam/emulsion/compare/v4.0.0...HEAD
