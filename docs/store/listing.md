# Microsoft Store listing

The text for the Partner Center submission, ready to paste. The screenshots next to this file are numbered in the order to
upload them (all larger than the 1366 × 768 minimum).

## Product

| Field | Value |
|---|---|
| Product name | Display Profiles |
| Category | Utilities & tools |
| Price | Free |
| Privacy policy URL | https://github.com/joshlove007/DisplayProfiles/blob/main/PRIVACY.md |
| Website | https://github.com/joshlove007/DisplayProfiles |
| Support contact | https://github.com/joshlove007/DisplayProfiles/issues |
| Copyright and trademark info | © 2026 Joshua Lovelace |
| License | MIT (https://github.com/joshlove007/DisplayProfiles/blob/main/LICENSE) |

## Short description

Save your monitor layouts as profiles and switch between them with one click, a shortcut, Xbox Game Bar or a controller.

## Description

Switching between "two monitors at my desk" and "the TV on the couch" in Windows means opening Display settings, enabling the
TV, making it the main display, and then doing it all again in reverse. Display Profiles turns each setup into a named profile
that you apply in one step, and it can do it automatically when your PC enters Xbox mode.

Make as many profiles as you like. Each one remembers which displays are on, which is the main display, and every display's
position, resolution, refresh rate and orientation. You can also choose to duplicate one display onto another instead of
extending the desktop.

Switch from anywhere:
• The profile editor, with a draggable monitor map just like Settings > Display
• The system tray icon
• Desktop and Start menu shortcuts, one per profile, or a single "next profile" shortcut
• The Display Profiles widget in Xbox Game Bar
• The command line (DisplayProfiles.exe)
• Automatically when the PC enters (or leaves) Xbox mode

Made for the couch: in Xbox mode the app switches to a large, full-screen layout that you can drive entirely with a controller,
including moving displays around.

Safe by design: Display Profiles checks that every display really came up (it waits for a slow TV to wake), puts your previous
layout back if Windows rejects a change, and asks "Keep these display settings?" when you apply from the app.

It looks and feels like part of Windows 11: WinUI 3, Mica, Segoe Fluent icons, your accent colour, and light or dark to match
your system. Profiles are stored in a plain JSON file in your AppData folder. No account, no ads, no data collected, and it
never uses the network.

## Features (up to 20, 200 characters each)

1. Save any number of display layouts as profiles and apply one in a single step
2. Drag displays on a snapping map like Windows Settings; drop one between two others to insert it
3. Choose which displays are on, the main display, resolution, refresh rate and orientation for each
4. Extend the desktop or duplicate a display, with shared settings for duplicated displays
5. Switch from the tray, shortcuts, the command line or the Xbox Game Bar widget
6. Apply a profile automatically when the PC enters Xbox mode, and another when it leaves
7. Full-screen controller mode you can use entirely with an Xbox controller
8. Checks every display came up, restores your layout on failure, and offers "Keep these display settings?"
9. Matches Windows 11 with Mica, Segoe Fluent icons and your system theme and accent colour
10. Private: no account, no network access, no data collected

## Search terms (up to 7, 30 characters each)

- display profiles
- monitor profiles
- switch monitors
- multiple monitors
- xbox mode
- duplicate displays
- tv monitor toggle

## What's new in this version

First Microsoft Store release. Save display layouts as profiles and switch between them from the app, the tray, shortcuts,
the command line, the Xbox Game Bar widget or a controller, and apply one automatically when the PC enters Xbox mode.

## Screenshots (upload in this order, with these captions)

| File | Caption |
|---|---|
| `1-profiles-light.png` | Save each display layout as a profile |
| `2-duplicate-displays.png` | Duplicate displays share their settings |
| `3-profiles-dark.png` | Matches your Windows theme, light or dark |
| `4-display-options.png` | Resolution, refresh rate, orientation and more for every display |
| `5-xbox-mode.png` | Pick the profiles to apply when Xbox mode starts and ends |
| `6-controller-mode.png` | A full-screen layout you can drive with a controller |
| `7-controller-move-display.png` | Move displays around with the D-pad |

## Age rating questionnaire

Category: **Utilities, productivity, news, weather or other**. Answer **No** to every question (violence, sexual content,
language, controlled substances, gambling, user-generated content, online interactions, location sharing, purchases and so on).
That gives the lowest rating (3+).

## Notes for certification

Paste this into *Notes for certification*:

> Display Profiles saves monitor layouts ("profiles") and applies them with the Windows display APIs (ChangeDisplaySettingsEx and
> SetDisplayConfig). It has no account, no sign-in, no ads and makes no network connections, so no test account is needed.
>
> The package declares the restricted capability runFullTrust. The app is a packaged WinUI 3 desktop app (Windows App SDK) and a
> desktop app is needed to change the display configuration of the PC. The package also contains an Xbox Game Bar widget.
>
> To test: open the app and select the Desk or TV profile (or press New profile). With a single monitor the editor still lets you
> create a profile, change resolution, refresh rate and orientation, and press Apply; Windows then asks "Keep these display
> settings?" before the change is kept. With two or more monitors you can also turn displays on and off, drag them on the map,
> choose which is the main display, and choose "Duplicate display N" under Display mode. Identify shows the number of each display
> on screen, and Refresh looks for the connected displays again.
>
> Xbox mode page: pick a profile to apply when Xbox mode starts. The app starts with Windows (a startup task you can turn off in
> Windows Settings > Apps > Startup or in the app's Settings page) so it can react to Xbox mode.
>
> Command line: `DisplayProfiles.exe help` lists the commands, for example `DisplayProfiles.exe list`.

## Justification for runFullTrust

> Display Profiles changes the PC's display configuration (which displays are on, the main display, their position, resolution,
> refresh rate and orientation) and runs in the background to react when Xbox mode starts. The Windows display configuration APIs
> and the background startup behaviour are not available to a sandboxed (UWP) app, so the app is a packaged full-trust desktop
> app. It reads and writes only its own settings file in %APPDATA%\DisplayProfiles.
