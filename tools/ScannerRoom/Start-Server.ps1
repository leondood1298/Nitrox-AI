[CmdletBinding()]
param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Subnautica',
    [ValidateRange(1, 65535)][int]$Port = 11000,
    [ValidatePattern('^\w+$')][string]$SaveName = 'ScannerAcceptance',
    [string]$RunRoot,
    [switch]$ResetData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

$packageRoot = Get-ScannerPackageRoot
$info = Get-ScannerBuildInfo -PackageRoot $packageRoot
Set-ScannerBundledRuntime -PackageRoot $packageRoot

$serverExe = Join-Path $packageRoot 'app\Nitrox.Server.Subnautica.exe'
if (-not (Test-Path -LiteralPath $serverExe -PathType Leaf)) {
    throw "Server executable is missing: $serverExe"
}
if (-not (Test-Path -LiteralPath $GamePath -PathType Container)) {
    throw "Subnautica game path is invalid: $GamePath"
}
$gameBuild = Assert-ScannerExpectedGameBuild -BuildInfo $info -GamePath $GamePath

$runsBase = Get-ScannerRunRoot -PackageId $info.PackageId -Override $RunRoot
$serverData = Assert-ScannerChildPath -Path (Join-Path $runsBase 'server') -Parent $runsBase
if ($ResetData -and (Test-Path -LiteralPath $serverData)) {
    Remove-Item -LiteralPath $serverData -Recurse -Force
}
New-Item -ItemType Directory -Path $serverData -Force | Out-Null

$fixture = Join-Path $packageRoot 'fixtures\scanner-room-v1'
$savePath = Join-Path (Join-Path $serverData 'saves') $SaveName
if (-not (Test-Path -LiteralPath $savePath) -and (Test-Path -LiteralPath $fixture -PathType Container)) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $savePath) -Force | Out-Null
    Copy-Item -LiteralPath $fixture -Destination $savePath -Recurse -Force
}

$runInfo = [ordered]@{
    PackageId = $info.PackageId
    Role = 'server'
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    DataPath = $serverData
    SaveName = $SaveName
    Port = $Port
    GamePath = $GamePath
}
$runInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $serverData 'scanner-run-info.json') -Encoding UTF8

Write-Host "Package: $($info.PackageId)"
Write-Host "Server data: $serverData"
Write-Host "Port: $Port"
Write-Host "Validated Subnautica build: $gameBuild"
Write-Host 'Use the server console commands `scannerroom` and `scannermark <test-id>` at checkpoints.'

$arguments = @(
    '--save', $SaveName,
    '--game-path', [System.IO.Path]::GetFullPath($GamePath),
    '--data-path', $serverData,
    '--assets-path', (Join-Path $packageRoot 'app'),
    "--GameServer:ServerPort=$Port",
    '--GameServer:PortForward=false',
    '--GameServer:LanDiscovery=false',
    '--GameServer:AutoSave=false',
    '--GameServer:SaveInterval=0',
    '--GameServer:MaxBackups=20',
    '--GameServer:Seed=95311395',
    '--GameServer:SerializerMode=JSON',
    '--GameServer:MaxConnections=3',
    '--GameServer:SafeBuilding=true'
)
& $serverExe @arguments
$serverExitCode = $LASTEXITCODE
if ($serverExitCode -ne 0) {
    throw "Nitrox server exited with code $serverExitCode."
}
return
