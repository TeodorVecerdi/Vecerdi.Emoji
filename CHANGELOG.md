# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2025-05-04

### Changed

- Refactored fallback logic to use a more efficient and consistent `EmojiFallbackResult` return type
- Added `EmojiProcessor.GetFallback` method to replace the legacy `GetFallbackEmojis` method
- Updated `EmojiProcessingManager` to use the new `GetFallback` method and leverage the `EmojiFallbackResult` return type
- Marked `GetFallbackEmojis` as obsolete
- Modified `EmojiProcessingManager` to use `ISet<EmojiCode>` instead of `HashSet<EmojiCode>` to increase flexibility


## [1.1.0] - 2025-05-04

### Changed

- Remove required dependency on `Vecerdi.Logging`
  - This can be enabled manually if preferred by adding `ENABLE_VECERDI_LOGGING` to scripting define symbols and adding the required NuGet and Unity package.

### Removed

- Remove sample for the Vecerdi.Logging dependency
## [1.0.0] - 2025-05-03

### Added

- Initial release of the Emoji Support package
- Core emoji processing functionality
- Emoji conversion utilities
- Fallback system for unsupported emoji
