# Display Profiles

Profile-based display switcher for Windows 11, written in C# on .NET 11.

Define several monitor layouts ("Desk", "TV", ...): which displays are on, which is the main display, resolution, refresh rate,
orientation and arrangement. Switch between them with one click, a shortcut, a command, the tray icon, or an Xbox controller
through Xbox Game Bar. A profile can also be applied automatically when the PC enters Xbox mode.

The app uses WinUI 3 and follows the system light/dark theme, accent colour and Segoe Fluent icons, so it looks like part of
Windows.

## Features

- **Profile editor** with a draggable, snapping monitor map (like Settings > Display), per-display on/off, main display,
  resolution, refresh rate and orientation. Edits save automatically.
- **Safe applying**: the app verifies that every monitor really came up (retrying if a TV is slow to wake), restores your
  previous layout if Windows rejects the change, and shows a "Keep these display settings?" prompt when you apply from the app.
- **Tray icon** with a profile menu, **command line** and **shortcuts** (`DisplayProfiles.exe apply "TV"`, `toggle`, `next`).
- **Xbox mode**: pick a profile to apply when Xbox mode starts (and optionally when it ends). Detection uses Windows'
  gaming-experience API.
- **Game Bar widget**: pick a profile with a controller from Xbox Game Bar.
- **Controller mode**: in Xbox mode the app switches to a dark, Xbox-green, full-screen layout with large controls and no
  window buttons, and everything can be done with a gamepad (see below).
- Imports the profiles of the original `toggle-monitor.ps1` script on first run.

## Build and run

Requires the [.NET 11 SDK](https://dotnet.microsoft.com/download) on Windows 11.

```powershell
dotnet build DisplayProfiles.slnx
dotnet test --solution DisplayProfiles.slnx
dotnet run --project src/DisplayProfiles.App
```

Everything above is SDK-style and works with VS Code (C# Dev Kit). `.vscode/tasks.json` has build, test and publish tasks.

### Command line

```text
DisplayProfiles.exe apply <profile>   switch to a profile (name, unique prefix, or id)
DisplayProfiles.exe toggle | next     switch to the next profile in the list
DisplayProfiles.exe list | current    show profiles / the active one
DisplayProfiles.exe capture <name>    save the current layout as a profile
DisplayProfiles.exe --background      start only the tray icon
DisplayProfiles.exe --page xbox       open the app on a page (profiles, xbox, settings)
DisplayProfiles.exe --xbox            force the controller layout (--desktop forces the normal one)
DisplayProfiles.exe --xbox --windowed controller layout in a normal window, handy for testing
```

Settings > Shortcuts creates a shortcut per profile on the desktop or in the Start menu. Pin one in the Game Bar
"LaunchDeck" widget if you prefer that to the DisplayProfiles widget.

### Xbox Game Bar widget

Game Bar can only host UWP XAML apps, and the UWP XAML compiler ships with Visual Studio's MSBuild, so the widget is the one
project that `dotnet build` can't build. It needs Visual Studio or the Build Tools with the **Universal Windows Platform build
tools** workload.

```powershell
.\tools\Build-Widget.ps1      # builds artifacts\widget\... (creates a self-signed signing certificate)
.\tools\Install-Widget.ps1    # trusts the certificate (asks for admin once) and installs the package
```

Then open Game Bar (Win + G), open the widget menu and add **DisplayProfiles**. The widget is sandboxed, so it never touches the
displays itself: the app publishes the profile list into the widget's data folder and registers a `displayprofiles:` link
(`displayprofiles:apply/<id>`, `displayprofiles:next`) that the widget launches to ask the app to switch.

### Xbox mode

Enable **Start with Windows** in Settings so the tray app is running when Xbox mode starts, then choose profiles on the
Xbox mode page.

### Controller mode

While the PC is in Xbox mode (or when Settings > Controller layout is set to *Always on*) the app uses a controller layout.
Any XInput gamepad works; the on-screen prompts at the bottom show what each button does.

| Button | Action |
|---|---|
| D-pad / left stick | Move focus |
| A | Select, toggle, expand, open a list |
| B | Back (closes a list, leaves the page, then hides the app) |
| Y | Apply the selected profile |
| LB / RB | Previous / next section |
| Right stick | Scroll |

To arrange displays, focus a monitor in the map and press **A** to pick it up, move it with the D-pad (it jumps between
positions that touch another display) and press **A** to drop it or **B** to cancel. Lists open as large pick-lists and the
"Keep these display settings?" prompt becomes a full-width overlay with a countdown.

The same actions work from a keyboard: arrows, Enter/Space, Esc, PageUp/PageDown and Y.

## Repository layout

| Path | Purpose |
|---|---|
| `src/DisplayProfiles.Core` | Display engine, profiles, CLI, Xbox mode detection, shortcuts, widget bridge |
| `src/DisplayProfiles.App` | WinUI 3 app: editor, tray icon, single-instance host |
| `src/DisplayProfiles.Widget*` | Game Bar widget and its Game Bar SDK projection (`DisplayProfiles.Widget.slnx`) |
| `tests/DisplayProfiles.Tests` | xUnit tests |
| `tools/` | Icon generator and widget build/install scripts |

## License

[MIT](LICENSE)
