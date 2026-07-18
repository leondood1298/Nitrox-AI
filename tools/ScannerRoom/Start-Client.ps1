[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerAddress,
    [ValidateRange(1, 65535)][int]$Port = 11000,
    [ValidateSet('A', 'B', 'C')][string]$MachineLabel = 'A',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Subnautica',
    [string]$RunRoot,
    [switch]$ResetData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

$packageRoot = Get-ScannerPackageRoot
$info = Get-ScannerBuildInfo -PackageRoot $packageRoot
Set-ScannerBundledRuntime -PackageRoot $packageRoot
if ([string]::IsNullOrWhiteSpace($ServerAddress) -or $ServerAddress -ne $ServerAddress.Trim() -or $ServerAddress -match '[\x00-\x20|]') {
    throw 'ServerAddress must be one hostname or raw IP address without whitespace, control characters, or the server-list delimiter.'
}
if ([System.Uri]::CheckHostName($ServerAddress) -eq [System.UriHostNameType]::Unknown) {
    throw 'ServerAddress is not a valid DNS hostname or raw IPv4/IPv6 address.'
}
[void](Assert-ScannerExpectedGameBuild -BuildInfo $info -GamePath $GamePath)

$launcher = Join-Path $packageRoot 'app\Nitrox.Launcher.exe'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Launcher executable is missing: $launcher"
}

$runsBase = Get-ScannerRunRoot -PackageId $info.PackageId -Override $RunRoot
$clientName = 'client-' + $MachineLabel.ToLowerInvariant()
$clientData = Assert-ScannerChildPath -Path (Join-Path $runsBase $clientName) -Parent $runsBase
if ($ResetData -and (Test-Path -LiteralPath $clientData)) {
    Remove-Item -LiteralPath $clientData -Recurse -Force
}
New-Item -ItemType Directory -Path $clientData -Force | Out-Null
"Scanner Acceptance|$ServerAddress|$Port" | Set-Content -LiteralPath (Join-Path $clientData 'servers') -Encoding ASCII

$runInfo = [ordered]@{
    PackageId = $info.PackageId
    Role = $clientName
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    DataPath = $clientData
    ServerAddress = $ServerAddress
    Port = $Port
}
$runInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $clientData 'scanner-run-info.json') -Encoding UTF8

Write-Host "Package: $($info.PackageId)"
Write-Host "Client data: $clientData"
Write-Host "Configured server: $ServerAddress`:$Port"
$launcherArguments = '--data-path "{0}"' -f $clientData
$process = Start-Process -FilePath $launcher -ArgumentList $launcherArguments -PassThru
Write-Host "Launcher PID: $($process.Id)"
Write-Host "Validated Subnautica path: $([System.IO.Path]::GetFullPath($GamePath))"
Write-Host 'Launcher isolation: --data-path is a supported launcher option for this build.'
Write-Output $clientData
