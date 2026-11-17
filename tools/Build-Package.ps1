<#
.SYNOPSIS
    Builds the combined MSIX: the Display Profiles app and its Xbox Game Bar widget in one package.

.DESCRIPTION
    The widget is a UWP app that has to be built with Visual Studio's MSBuild (see Build-Widget.ps1); the app is a
    full-trust WinUI 3 desktop app built with `dotnet publish`. Both target the same .NET runtime, so their files
    merge into one package layout. This script builds both, merges the layouts and manifests, merges the resource
    indexes with MakePri, then packs it. Unless -NoSign is given, the package is signed with the self-signed certificate
    Build-Widget.ps1 creates for the default publisher. A release build passes the publisher of its real certificate
    (it must equal the certificate's subject) and -NoSign, and signs the package in a later step.

    Needs the Windows SDK (makeappx, makepri, signtool) plus everything Build-Widget.ps1 needs.

.EXAMPLE
    .\tools\Build-Package.ps1

.EXAMPLE
    .\tools\Build-Package.ps1 -SkipWidgetBuild -Version 0.1.0.7 -NoSign `
        -Publisher 'CN=Joshua Lovelace, O=Joshua Lovelace, L=Hendersonville, S=tn, C=US' -PublisherDisplayName 'Joshua Lovelace'
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',

    [string]$Version = '0.1.0.0',

    [string]$Name = 'DisplayProfiles',

    [string]$Publisher = 'CN=DisplayProfiles',

    [string]$PublisherDisplayName = 'Display Profiles',

    [switch]$SkipWidgetBuild,

    [switch]$NoSign
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$widgetDir = Join-Path $repoRoot 'artifacts\widget'
$outDir = Join-Path $repoRoot 'artifacts\package'
$stage = Join-Path $repoRoot "artifacts\package-stage\$Platform"

$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
if (-not $sdkBin) { throw 'The Windows SDK (makeappx.exe) was not found.' }
$tools = Join-Path $sdkBin.FullName 'x64'

if (-not $SkipWidgetBuild) {
    & (Join-Path $PSScriptRoot 'Build-Widget.ps1') -Platform $Platform
}

$widgetMsix = Get-ChildItem $widgetDir -Recurse -Filter "DisplayProfiles.Widget_*_${Platform}.msix" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $widgetMsix) { throw "No widget package found under $widgetDir. Run Build-Widget.ps1 first." }

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null

# Widget layout first, then the app on top. Files both have (the shared .NET runtime and WinAppSDK pieces) are identical.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($widgetMsix.FullName, $stage)
$platformArg = if ($Platform -eq 'ARM64') { '-p:Platform=ARM64' } else { '-p:Platform=x64' }
dotnet publish (Join-Path $repoRoot 'src\DisplayProfiles.App') -c Release -r $rid $platformArg -o $stage --nologo -v:m
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# makeappx writes these itself; the widget's own resource index is folded into the merged one below.
Remove-Item (Join-Path $stage 'AppxBlockMap.xml'), (Join-Path $stage '[Content_Types].xml'), (Join-Path $stage 'AppxSignature.p7x'), (Join-Path $stage 'AppxMetadata') -Recurse -ErrorAction SilentlyContinue
Move-Item (Join-Path $stage 'resources.pri') (Join-Path $stage 'DisplayProfiles.Widget.pri') -Force

# Manifest: the widget's generated manifest plus the full-trust app and what it needs.
$manifestPath = Join-Path $stage 'AppxManifest.xml'
$xml = [xml](Get-Content $manifestPath -Raw)
$package = $xml.Package
$foundation = $package.NamespaceURI
$uri = @{
    uap      = 'http://schemas.microsoft.com/appx/manifest/uap/windows10'
    uap3     = 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3'
    uap5     = 'http://schemas.microsoft.com/appx/manifest/uap/windows10/5'
    desktop  = 'http://schemas.microsoft.com/appx/manifest/desktop/windows10'
    rescap   = 'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities'
}
foreach ($prefix in 'desktop', 'rescap', 'uap5') { $package.SetAttribute("xmlns:$prefix", $uri[$prefix]) }
$package.SetAttribute('IgnorableNamespaces', "$($package.GetAttribute('IgnorableNamespaces')) desktop rescap uap5".Trim())

$package.Identity.Name = $Name
$package.Identity.Publisher = $Publisher
$package.Identity.Version = $Version
$package.Properties.DisplayName = 'Display Profiles'
$package.Properties.PublisherDisplayName = $PublisherDisplayName

# Full-trust apps need the desktop device family.
foreach ($family in @($package.Dependencies.TargetDeviceFamily | Where-Object Name -eq 'Windows.Universal')) {
    $package.Dependencies.RemoveChild($family) | Out-Null
}

function New-Element($name, $ns, $attributes = @{}, $text = $null) {
    $element = $xml.CreateElement($name, $ns)
    foreach ($key in $attributes.Keys) { $element.SetAttribute($key, $attributes[$key]) }
    if ($text) { $element.InnerText = $text }
    $element
}

$app = New-Element 'Application' $foundation @{ Id = 'DisplayProfiles'; Executable = 'DisplayProfiles.exe'; EntryPoint = 'Windows.FullTrustApplication' }
$visuals = New-Element 'uap:VisualElements' $uri.uap @{
    DisplayName = 'Display Profiles'; Description = 'Save display layouts and switch between them'
    Square150x150Logo = 'Assets\Square150x150Logo.png'; Square44x44Logo = 'Assets\Square44x44Logo.png'; BackgroundColor = 'transparent' }
$app.AppendChild($visuals) | Out-Null

$extensions = New-Element 'Extensions' $foundation
$protocol = New-Element 'uap:Extension' $uri.uap @{ Category = 'windows.protocol' }
$protocolName = New-Element 'uap:Protocol' $uri.uap @{ Name = 'displayprofiles' }
$protocolName.AppendChild((New-Element 'uap:DisplayName' $uri.uap @{} 'Display Profiles')) | Out-Null
$protocol.AppendChild($protocolName) | Out-Null
$extensions.AppendChild($protocol) | Out-Null

$startup = New-Element 'uap5:Extension' $uri.uap5 @{ Category = 'windows.startupTask'; Executable = 'DisplayProfiles.exe'; EntryPoint = 'Windows.FullTrustApplication' }
$startup.AppendChild((New-Element 'uap5:StartupTask' $uri.uap5 @{ TaskId = 'DisplayProfilesStartup'; Enabled = 'true'; DisplayName = 'Display Profiles' })) | Out-Null
$extensions.AppendChild($startup) | Out-Null

$alias = New-Element 'uap3:Extension' $uri.uap3 @{ Category = 'windows.appExecutionAlias'; Executable = 'DisplayProfiles.exe'; EntryPoint = 'Windows.FullTrustApplication' }
$aliasBody = New-Element 'uap3:AppExecutionAlias' $uri.uap3
$aliasBody.AppendChild((New-Element 'desktop:ExecutionAlias' $uri.desktop @{ Alias = 'DisplayProfiles.exe' })) | Out-Null
$alias.AppendChild($aliasBody) | Out-Null
$extensions.AppendChild($alias) | Out-Null
$app.AppendChild($extensions) | Out-Null

$package.Applications.InsertBefore($app, $package.Applications.FirstChild) | Out-Null

# The generated manifest carries an empty, misplaced Capabilities element; replace it.
foreach ($existing in @($package.ChildNodes | Where-Object LocalName -eq 'Capabilities')) { $package.RemoveChild($existing) | Out-Null }
$capabilities = New-Element 'Capabilities' $foundation
$package.InsertAfter($capabilities, $package.Applications) | Out-Null
$capabilities.AppendChild((New-Element 'rescap:Capability' $uri.rescap @{ Name = 'runFullTrust' })) | Out-Null
$xml.Save($manifestPath)

# One resource index for the whole package (the app's, the widget's and the WinUI controls'). Only the existing .pri
# files are indexed: scanning the folder as well would register every compiled .xbf a second time and conflict.
$priFiles = Get-ChildItem $stage -Filter *.pri
$indexes = $priFiles | ForEach-Object {
    "<index root=`"\`" startIndexAt=`"\$($_.Name)`"><default><qualifier name=`"Language`" value=`"en-US`"/></default><indexer-config type=`"PRI`"/></index>"
}
$priConfig = Join-Path (Split-Path $stage -Parent) 'priconfig.xml'
Set-Content $priConfig "<?xml version=`"1.0`" encoding=`"UTF-8`"?><resources targetOsVersion=`"10.0.0`" majorVersion=`"1`">$($indexes -join '')</resources>"
$resourcesPri = Join-Path $stage 'resources.pri'
& (Join-Path $tools 'makepri.exe') new /pr $stage /cf $priConfig /mn $manifestPath /of $resourcesPri /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed.' }
# Only the widget's own index is folded into resources.pri; the other .pri files stay for whatever loads them by file.
Remove-Item (Join-Path $stage 'DisplayProfiles.Widget.pri')
New-Item -ItemType Directory -Force $outDir | Out-Null
$msix = Join-Path $outDir "DisplayProfiles_${Version}_$Platform.msix"
& (Join-Path $tools 'makeappx.exe') pack /d $stage /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }

if ($NoSign) {
    Write-Host "`nUnsigned package written to $msix" -ForegroundColor Yellow
    return
}

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object Subject -eq $Publisher | Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) { throw "No code-signing certificate for '$Publisher'. Run Build-Widget.ps1 once to create it, or use -NoSign." }
& (Join-Path $tools 'signtool.exe') sign /fd SHA256 /sha1 $cert.Thumbprint $msix | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'signtool sign failed.' }
Export-Certificate -Cert $cert -FilePath ([IO.Path]::ChangeExtension($msix, '.cer')) | Out-Null

Write-Host "`nPackage written to $msix" -ForegroundColor Green
