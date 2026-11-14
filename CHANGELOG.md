# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the
project uses [semantic versioning](https://semver.org/).

## [Unreleased]

## [0.1.0-beta.1]

First public beta.

### Added

- Profile editor with a draggable monitor map, per-display on/off, main display, resolution, refresh rate and orientation.
- Safe applying: verifies that every monitor came up, retries slow TVs, restores the previous layout on failure, and a
  "Keep these display settings?" prompt.
- Tray icon, command line (`apply`, `toggle`, `list`, `current`, `capture`), shortcuts and `displayprofiles:` links.
- Xbox mode: apply a profile when Xbox mode starts and another when it ends.
- Controller mode: a large, full-screen layout for Xbox mode that works entirely with a gamepad, including moving displays,
  with Dark, Light and Classic themes.
- Desktop themes: Dark, Light and Automatic (System).
- Xbox Game Bar widget for switching profiles with a controller.
- Responsive layout that adapts to narrow windows.
- Installers for x64 and arm64 (per user), portable zips and the Game Bar widget package.

[Unreleased]: https://github.com/joshlove007/DisplayProfiles/compare/v0.1.0-beta.1...HEAD
[0.1.0-beta.1]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.1
