<#
.SYNOPSIS
    Builds the Xbox Game Bar widget as a signed, sideloadable MSIX.

.DESCRIPTION
    Game Bar only hosts UWP XAML apps, and the UWP XAML compiler ships with Visual Studio's MSBuild, so the widget can't
    be built with `dotnet build`. This script finds MSBuild through vswhere, creates (or reuses) a self-signed code-signing
    certificate whose subject matches the manifest publisher, and builds into artifacts\widget.

    Needs Visual Studio (or the Build Tools) with the "Universal Windows Platform build tools" workload.

.EXAMPLE
    .\tools\Build-Widget.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'src\DisplayProfiles.Widget\DisplayProfiles.Widget.csproj'
$manifest = Join-Path $repoRoot 'src\DisplayProfiles.Widget\Package.appxmanifest'
$outDir = Join-Path $repoRoot 'artifacts\widget'

$publisher = ([xml](Get-Content $manifest)).Package.Identity.Publisher

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found. Install the Visual Studio Build Tools with the UWP build tools workload.' }
$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsPath) { throw 'No Visual Studio installation with MSBuild was found.' }
$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) { throw "MSBuild.exe not found under '$vsPath'." }

# The package signature's subject must equal the manifest publisher, or Windows rejects the package.
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object Subject -eq $publisher |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating a self-signed certificate for '$publisher'..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher `
        -KeyUsage DigitalSignature -FriendlyName 'DisplayProfiles widget sideload signing' `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
Write-Host "Signing with $($cert.Subject) ($($cert.Thumbprint))"

$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
New-Item -ItemType Directory -Force $outDir | Out-Null

& $msbuild $project -restore -nologo -v:m `
    -p:Configuration=Release `
    "-p:Platform=$Platform" `
    "-p:RuntimeIdentifier=$rid" `
    -p:SelfContained=true `
    -p:GenerateAppxPackageOnBuild=true `
    "-p:AppxPackageDir=$outDir\" `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxBundle=Never `
    -p:AppxPackageSigningEnabled=true `
    "-p:PackageCertificateThumbprint=$($cert.Thumbprint)"
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

Write-Host "`nWidget package written to $outDir" -ForegroundColor Green
Write-Host 'Install it with: .\tools\Install-Widget.ps1'
