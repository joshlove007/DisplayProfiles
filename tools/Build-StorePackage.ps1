<#
.SYNOPSIS
    Builds the Microsoft Store submission: one unsigned .msixbundle holding the x64 and ARM64 packages.

.DESCRIPTION
    The Store gives the package its own identity (name and publisher) and signs it itself, so this uses the identity from
    Partner Center (Product management > Product identity) and doesn't sign anything. The GitHub release packages use a
    different identity and Azure Artifact Signing; see Build-Package.ps1.

    The Store reserves the last part of the version, so it must be 0 and the first three parts must go up with every
    submission.

.EXAMPLE
    .\tools\Build-StorePackage.ps1 -Version 0.1.1.0
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')]
    [string]$Version = '0.1.0.0',

    # Copy these three from Partner Center > Product management > Product identity.
    [string]$Name = 'JoshuaLovelace.DisplayProfiles',
    [string]$Publisher = 'CN=73CB8E95-5398-440D-8439-BE6488133590',
    [string]$PublisherDisplayName = 'Joshua Lovelace',

    [switch]$SkipWidgetBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$packages = Join-Path $repoRoot 'artifacts\package'
$outDir = Join-Path $repoRoot "artifacts\store\$Version"
Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $outDir | Out-Null

foreach ($platform in 'x64', 'ARM64') {
    if (-not $SkipWidgetBuild) {
        & (Join-Path $PSScriptRoot 'Build-Widget.ps1') -Platform $platform
    }

    & (Join-Path $PSScriptRoot 'Build-Package.ps1') -Platform $platform -SkipWidgetBuild -NoSign -Version $Version `
        -Name $Name -Publisher $Publisher -PublisherDisplayName $PublisherDisplayName
    Copy-Item (Join-Path $packages "DisplayProfiles_${Version}_$platform.msix") $outDir
}

$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
    Sort-Object { [version]$_.Name } | Select-Object -Last 1
$bundle = Join-Path $repoRoot "artifacts\store\DisplayProfiles_$Version.msixbundle"
& (Join-Path $sdkBin.FullName 'x64\makeappx.exe') bundle /d $outDir /bv $Version /p $bundle /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed.' }

Write-Host "`nStore package written to $bundle" -ForegroundColor Green
