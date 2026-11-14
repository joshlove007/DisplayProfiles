; Per-user installer for Display Profiles. No administrator rights needed.
; Build:  iscc /DAppVersion=0.1.0-beta.1 /DNumericVersion=0.1.0.0 /DArch=x64 /DSourceDir=..\publish\win-x64 installer\DisplayProfiles.iss
; (the release workflow does this for x64 and arm64)

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\win-x64"
#endif

#define AppName "Display Profiles"
#define AppExe "DisplayProfiles.exe"

[Setup]
AppId={{6F2C7B0E-5B7E-4B8F-9D0A-3A1D2C4E8F11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Josh Lovelace
AppPublisherURL=https://github.com/joshlove007/DisplayProfiles
AppSupportURL=https://github.com/joshlove007/DisplayProfiles/issues
VersionInfoVersion={#NumericVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=DisplayProfiles-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\src\DisplayProfiles.App\Assets\DisplayProfiles.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE

[Tasks]
Name: "startup"; Description: "Start Display Profiles when I sign in (needed to react to Xbox mode)"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the app's Settings > Start with Windows writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplayProfiles"; ValueData: """{app}\{#AppExe}"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ask a running copy to exit so its files can be removed.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden skipifdoesntexist; RunOnceId: "StopApp"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // The app registers its own link and start-up entry at run time, so remove them here as well.
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\displayprofiles');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DisplayProfiles');
  end;
end;
