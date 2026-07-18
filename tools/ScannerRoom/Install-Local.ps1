[CmdletBinding()]
param(
    [string]$PackageRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$InstallBase,
    [switch]$Refresh
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

$source = [System.IO.Path]::GetFullPath($PackageRoot)
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $source -Quiet
$info = Get-ScannerBuildInfo -PackageRoot $source
if ([string]::IsNullOrWhiteSpace($InstallBase)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw 'LOCALAPPDATA is unavailable; pass -InstallBase explicitly.'
    }
    $InstallBase = Join-Path $env:LOCALAPPDATA 'Nitrox-AI-TestBuilds'
}
$installBasePath = [System.IO.Path]::GetFullPath($InstallBase)
$destination = Assert-ScannerChildPath -Path (Join-Path $installBasePath $info.PackageId) -Parent $installBasePath
if ($source.TrimEnd('\').Equals($destination.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Source package and local installation paths must be different.'
}
$trustedManifest = Join-Path $source 'SHA256SUMS.txt'
$trustedManifestHash = (Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash
$syncedInbox = Join-Path $source 'evidence-inbox'

if (Test-Path -LiteralPath $destination) {
    if (-not $Refresh) {
        if ((Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash -ne $trustedManifestHash) {
            throw 'The verified source manifest changed before destination verification; wait for sync to settle and retry.'
        }
        & (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $destination -TrustedManifestPath $trustedManifest -Quiet
        if ((Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash -ne $trustedManifestHash) {
            throw 'The verified source manifest changed during destination verification; retry from a stable source.'
        }
        $env:NITROX_SCANNER_EVIDENCE_INBOX = $syncedInbox
        Write-Host "Already installed and verified: $destination"
        Write-Host "Synced evidence inbox: $syncedInbox"
        Write-Output $destination
        return
    }
    if ((Get-Item -LiteralPath $destination -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to replace a reparse-point installation: $destination"
    }
    Remove-Item -LiteralPath $destination -Recurse -Force
}

New-Item -ItemType Directory -Path $installBasePath -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
if ((Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash -ne $trustedManifestHash) {
    throw 'The verified source manifest changed during local installation; discard the partial copy and retry after sync settles.'
}
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $destination -TrustedManifestPath $trustedManifest -Quiet
if ((Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash -ne $trustedManifestHash) {
    throw 'The verified source manifest changed during destination verification; retry from a stable source.'
}
$env:NITROX_SCANNER_EVIDENCE_INBOX = $syncedInbox
Write-Host "Installed and verified: $destination"
Write-Host "Synced evidence inbox: $syncedInbox"
Write-Output $destination
