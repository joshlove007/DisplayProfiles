# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the
project uses [semantic versioning](https://semver.org/).

## [Unreleased]

### Added

- A single MSIX package that contains the app and the Xbox Game Bar widget (x64). It is signed with Azure Artifact Signing, so
  it installs without importing a certificate. Start with Windows, the `displayprofiles:` link, the command line and the
  shortcuts use the package's own mechanisms when packaged.

### Changed

- The app's `App.xaml` is now `MainApp.xaml`, and the widget builds on .NET 11.

## [0.1.0-beta.3]

### Added

- Save button: edits to a profile stay in memory until you save them (Ctrl+S, or X on a controller). Unsaved profiles show
  a dot and switching away asks to save or discard.
- Setting to show displays that are saved in a profile, off and not connected. They are hidden by default and can be
  removed from a profile.
- The installer can install the Xbox Game Bar widget.

### Changed

- Display numbers are list positions (1, 2, 3 ...) and no longer follow Windows' adapter numbers, so there are no gaps.
- The Game Bar widget is named "Display Profiles for Xbox Game Bar", no longer shows a second title, and is hidden from
  the Start menu.
- Page content is centred in wide windows; the profile list stays on the left.

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

[Unreleased]: https://github.com/joshlove007/DisplayProfiles/compare/v0.1.0-beta.3...HEAD
[0.1.0-beta.3]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.3
[0.1.0-beta.1]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.1
