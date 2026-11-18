# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the
project uses [semantic versioning](https://semver.org/).

## [Unreleased]

### Added

- Each release has a signed `DisplayProfiles-<version>.msixbundle` with the x64 and ARM64 packages in one file, next to the two
  separate packages.

### Changed

- The unsigned Microsoft Store bundle is now attached to each GitHub release (named `...-store-<version>.msixbundle`, for
  uploading in Partner Center, not for installing) as well as kept as a run artifact.

## [0.1.0-beta.7]

### Changed

- The release workflow also builds the unsigned Microsoft Store bundle and keeps it as a run artifact, so it no longer has to be built by hand.
  Running the workflow again for a tag that already has a release now builds only the Store bundle instead of failing.

### Fixed

- Changing a setting and then changing it back no longer leaves the profile marked as unsaved. The editor compares your edits
  with the saved profile, so Save stays off and switching profiles doesn't ask about changes that aren't there.
- On laptops with two graphics chips, Windows can name the wrong monitor for an output: an output that was driving the TV
  was reported as the laptop's own panel. The editor then showed the TV's resolution and settings on the panel, listed the
  TV as not connected, and Identify put the wrong number on the TV. Display Profiles now asks the display configuration
  which monitor each active output really shows.
- A display that isn't on the desktop (the TV while the Desk profile is active) only offered one resolution, or a short generic
  list, in the editor. Windows only reports a monitor's full list while the monitor is on the desktop, so Display Profiles now
  remembers the list each monitor had the last time it was on (`modes.json` next to your profiles) and shows it while the
  monitor is off. A monitor it has never seen on the desktop falls back to what the monitor says it supports (its EDID).
- Refresh rates are named like Windows Settings: 119.88, 59.94, 29.97 and 23.98 Hz instead of 119, 59, 29 and 23. The
  resolution list is sorted the same way as Windows too (widest first).

## [0.1.0-beta.6]

### Added

- **Refresh displays** button next to Identify, to look for the connected displays again.

### Changed

- Duplicated displays now all show their settings. Resolution, refresh rate and orientation are shared across the group, and the
  resolution and refresh rate lists only offer what every display in the group supports, as in Windows Settings. "Display
  mode" names the other displays in the group, such as "Duplicate display 1 and 4", before you pick it.
- The map shows a duplicated group as one tile such as "1|3".
- Displays always snap together on the map and can't be left with gaps. Drag a display between two others to insert it there;
  the layout updates when you let go, and a marker shows where it will land.
- Identify appears instantly and looks like the one in Windows Settings: a dark rounded box in the bottom-left corner of each
  display, with one box for a duplicated group.

### Fixed

- Duplicating a display onto one that is connected to a different graphics adapter (for example a TV on the GPU and monitors on
  the processor graphics) failed even though Windows Settings could do it.
- Changing a display between extend and duplicate didn't update its resolution list until you switched profiles.

## [0.1.0-beta.5]

### Fixed

- Windows sometimes leaves a connected monitor that isn't on the desktop out of its display list (for example after duplicating
  displays). Display Profiles then said it wasn't connected and skipped it. It now sees these monitors through the display
  configuration and extends the desktop first when a profile needs one.

## [0.1.0-beta.4]

### Added

- Each display has a **Multiple displays** setting: keep the default "Extend desktop to this display", or choose "Duplicate
  display N" to show the same picture as another display. Duplicates work in profiles, from the command line, the widget and
  Xbox mode. The map shows a mirrored pair as one tile such as "1 | 3".
- Start with Windows is on by default for new installs.
- A single MSIX package for x64 and ARM64 that contains the app and the Xbox Game Bar widget. It is signed with Azure Artifact
  Signing, so it installs with a double-click and no certificate to import. Start with Windows, the `displayprofiles:` link,
  the command line (`DisplayProfiles.exe`) and shortcuts use the package's own mechanisms.

### Changed

- The MSIX package replaces the installers, the portable zips and the separate widget package.
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

[Unreleased]: https://github.com/joshlove007/DisplayProfiles/compare/v0.1.0-beta.5...HEAD
[0.1.0-beta.5]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.5
[0.1.0-beta.4]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.4
[0.1.0-beta.3]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.3
[0.1.0-beta.1]: https://github.com/joshlove007/DisplayProfiles/releases/tag/v0.1.0-beta.1
