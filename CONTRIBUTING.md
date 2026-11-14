# Contributing

Thanks for wanting to help. Bug reports, ideas and pull requests are all welcome.

## Reporting a problem

Open an [issue](https://github.com/joshlove007/DisplayProfiles/issues/new/choose). For display problems it helps a lot to
include your Windows version, the monitors involved and how they are connected (HDMI, DisplayPort, a dock), plus the output
of `DisplayProfiles.exe list`.

## Setting up

You need Windows 11 and the [.NET 11 SDK](https://dotnet.microsoft.com/download) (`global.json` pins the version).

```powershell
git clone https://github.com/joshlove007/DisplayProfiles.git
cd DisplayProfiles
dotnet build DisplayProfiles.slnx
dotnet test --solution DisplayProfiles.slnx
dotnet run --project src/DisplayProfiles.App
```

VS Code with C# Dev Kit works out of the box. The Game Bar widget needs the Visual Studio Build Tools with the
**Universal Windows Platform** workload; see the README for `tools\Build-Widget.ps1`.

A few things that save time:

- A running `DisplayProfiles.exe` locks its build output. Close it (or exit it from the tray) before building.
- `--xbox --windowed` shows the controller layout in a normal window, and the keyboard works as a controller there (arrows,
  Enter, Esc, PageUp/PageDown, Y).
- Applying a profile changes your real displays. Keep the "Keep these display settings?" prompt in mind when testing.

## Making a change

`main` is protected, so every change goes through a pull request:

1. Create a branch from `main` (`feature/...`, `fix/...`, `docs/...`).
2. Make the change. Keep it focused, and add or update tests for behaviour you change.
3. Run `dotnet build` and `dotnet test`. The build treats warnings as errors.
4. Push and open a pull request. CI must pass before it can be merged, and pull requests are squash-merged.

### Code style

`.editorconfig` and the analyzers enforce most of it. In short: nullable reference types on, file-scoped namespaces, no
comments that just restate the code, and logic that does not need a UI lives in `DisplayProfiles.Core` where it can be
unit-tested.

### Screenshots

If you change the UI, please update the screenshots in `docs/images`. They are plain PNGs of the app window.

## Releases

Maintainers release by pushing a tag like `v0.2.0` (or `v0.2.0-beta.1` for a pre-release). The
[release workflow](.github/workflows/release.yml) does the rest.

## License

By contributing you agree that your contributions are licensed under the [MIT license](LICENSE).
