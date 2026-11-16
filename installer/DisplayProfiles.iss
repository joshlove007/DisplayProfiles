; Per-user installer for Display Profiles. No administrator rights needed for the app itself.
; Build:  iscc /DAppVersion=0.1.0-beta.1 /DNumericVersion=0.1.0.0 /DArch=x64 /DSourceDir=..\publish\win-x64 [/DWidgetDir=..\widget-stage] installer\DisplayProfiles.iss
; With /DWidgetDir (a folder holding the widget .msix, its .cer and Install-Widget.ps1) the installer can also install the
; Xbox Game Bar widget. The release workflow does this for the x64 installer.

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

#ifndef AppIdValue
  ; Test builds can pass another id (/DAppIdValue=...) so they don't replace the real installation's record.
  #define AppIdValue "{{6F2C7B0E-5B7E-4B8F-9D0A-3A1D2C4E8F11}"
#endif

#define AppName "Display Profiles"
#define AppExe "DisplayProfiles.exe"

[Setup]
AppId={#AppIdValue}
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
#ifdef WidgetDir
Name: "widget"; Description: "Install the Xbox Game Bar widget (asks for administrator permission once, to trust its certificate)"; GroupDescription: "Options:"
#endif

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef WidgetDir
Source: "{#WidgetDir}\*"; DestDir: "{app}\GameBarWidget"; Flags: ignoreversion; Tasks: widget
#endif

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
#ifdef WidgetDir
// The widget is a package that Windows only accepts once its certificate is trusted, which needs administrator rights.
// Install-Widget.ps1 (shipped next to the package) does both and asks for that permission itself.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  WidgetFolder, PowerShell, LogFile, CommandLine: String;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('widget') then
  begin
    WidgetFolder := ExpandConstant('{app}\GameBarWidget');
    PowerShell := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    LogFile := WidgetFolder + '\install.log';
    WizardForm.StatusLabel.Caption := 'Installing the Xbox Game Bar widget...';

    // PSModulePath is cleared so Windows PowerShell never picks up another PowerShell's modules from the environment.
    CommandLine := '/S /C "set "PSModulePath=" & "' + PowerShell + '" -NoProfile -ExecutionPolicy Bypass -File "' +
      WidgetFolder + '\Install-Widget.ps1" > "' + LogFile + '" 2>&1"';
    if not Exec(ExpandConstant('{cmd}'), CommandLine, WidgetFolder, SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    begin
      SuppressibleMsgBox('The Xbox Game Bar widget could not be installed. Display Profiles itself is installed. ' +
        'You can try the widget again later by running Install-Widget.ps1 in ' + WidgetFolder + '. ' +
        'Details are in ' + LogFile + '.',
        mbInformation, MB_OK, IDOK);
    end;
  end;
end;
#endif

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  // Remove the Game Bar widget if it was installed (nothing happens if it wasn't).
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -Command "Get-AppxPackage DisplayProfiles.GameBarWidget | Remove-AppxPackage"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  // The app registers its own link and start-up entry at run time, so remove them here as well.
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\displayprofiles');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DisplayProfiles');
  end;
end;
