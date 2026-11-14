<#
.SYNOPSIS
    Installs the DisplayProfiles Game Bar widget built by Build-Widget.ps1.

.DESCRIPTION
    Trusts the package's self-signed certificate (this needs administrator rights, so the script asks for them once)
    and installs the MSIX for the current user. Use -Uninstall to remove the widget again.
#>
[CmdletBinding()]
param(
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$packageName = 'DisplayProfiles.GameBarWidget'

# The widget used to be called MonitorToggle.
Get-AppxPackage 'MonitorToggle.GameBarWidget' | Remove-AppxPackage -ErrorAction SilentlyContinue

if ($Uninstall) {
    Get-AppxPackage $packageName | Remove-AppxPackage
    Write-Host 'Widget removed.' -ForegroundColor Green
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$msix = Get-ChildItem (Join-Path $repoRoot 'artifacts\widget') -Filter '*.msix' -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) { throw 'No .msix found in artifacts\widget. Run tools\Build-Widget.ps1 first.' }
$cer = Get-ChildItem $msix.Directory.FullName -Filter '*.cer' | Select-Object -First 1
if (-not $cer) { throw "No .cer next to $($msix.Name)." }

$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $cer.FullName
$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $certificate.Thumbprint
if (-not $trusted) {
    Write-Host 'Trusting the widget certificate (administrator rights needed)...' -ForegroundColor Yellow
    $command = "Import-Certificate -FilePath '$($cer.FullName)' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
    Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile', '-Command', $command
}

$msixVersion = [regex]::Match($msix.Name, '_(\d+\.\d+\.\d+\.\d+)_').Groups[1].Value
$installed = Get-AppxPackage $packageName
if ($installed -and $installed.Version -eq $msixVersion) {
    # Windows blocks reinstalling different contents under the same version, so remove the old build first.
    Write-Host "Removing the installed $msixVersion build first..." -ForegroundColor Yellow
    $installed | Remove-AppxPackage
}

$dependencies = Get-ChildItem (Join-Path $msix.Directory.FullName 'Dependencies\x64') -Filter '*.appx' -ErrorAction SilentlyContinue |
    ForEach-Object FullName

if ($dependencies) {
    Add-AppxPackage -Path $msix.FullName -DependencyPath $dependencies -ForceApplicationShutdown
}
else {
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
}

Write-Host "Installed $($msix.Name)." -ForegroundColor Green
Write-Host 'Open Game Bar (Win + G), open the widget menu and add DisplayProfiles. Sign out and in if it does not show up.'
