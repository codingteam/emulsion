# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic
Versioning v2.0.0](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Changed
- Additional logging for mailbox processor errors

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
[Unreleased]: https://github.com/codingteam/emulsion/compare/v1.7.0...HEAD
