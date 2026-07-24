[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z][A-Za-z0-9-]*$')][string]$TestId,
    [Parameter(Mandatory = $true)][ValidateSet('before', 'after', 'pass', 'fail', 'note')][string]$Phase,
    [ValidateSet('server', 'A', 'B', 'C')][string]$Machine = 'server',
    [string]$Note = '',
    [string]$DataPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

$packageRoot = Get-ScannerPackageRoot
$info = Get-ScannerBuildInfo -PackageRoot $packageRoot
if ([string]::IsNullOrWhiteSpace($DataPath)) {
    $runsBase = Get-ScannerRunRoot -PackageId $info.PackageId
    $roleFolder = if ($Machine -eq 'server') { 'server' } else { 'client-' + $Machine.ToLowerInvariant() }
    $DataPath = Join-Path $runsBase $roleFolder
}
New-Item -ItemType Directory -Path $DataPath -Force | Out-Null

$cleanNote = (Protect-ScannerEvidenceText -Text ($Note -replace '[\r\n|]', ' ')).Trim()
$line = 'TEST|{0}|machine={1}|id={2}|phase={3}|note={4}' -f [DateTime]::UtcNow.ToString('o'), $Machine, $TestId.ToUpperInvariant(), $Phase, $cleanNote
Add-Content -LiteralPath (Join-Path $DataPath 'scanner-test-marks.log') -Value $line -Encoding UTF8
Write-Host $line
