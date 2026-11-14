# Security policy

## Reporting a vulnerability

Please report security problems privately through GitHub's
[private vulnerability reporting](https://github.com/joshlove007/DisplayProfiles/security/advisories/new) instead of opening
a public issue. You will get a reply as soon as I can manage one.

## Scope

Display Profiles runs as your user. It does not need administrator rights, makes no network connections, and only changes
display settings and its own files under `%APPDATA%\DisplayProfiles`, plus a start-up entry and a `displayprofiles:` link
under `HKCU` when those features are used.

The Game Bar widget package is signed with a self-signed certificate that you are asked to trust once. The release build
signs every release with the same certificate. If you would rather not trust it, you can skip the widget or build it yourself
with `tools\Build-Widget.ps1`, which creates a certificate on your own machine.
