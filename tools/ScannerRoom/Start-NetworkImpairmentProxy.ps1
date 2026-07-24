[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerAddress,
    [ValidateRange(1, 65535)][int]$ServerPort = 11000,
    [string]$ListenAddress = '0.0.0.0',
    [ValidateRange(1, 65535)][int]$ListenPort = 11001,
    [ValidateRange(0, 60000)][int]$DelayMilliseconds = 120,
    [ValidateRange(0, 60000)][int]$JitterMilliseconds = 30,
    [ValidateRange(0, 50)][decimal]$LossPercent = 2.00,
    [ValidateRange(0, 1000000)][int]$ReorderEvery = 20,
    [ValidateRange(1, 60000)][int]$ReorderHoldMilliseconds = 250,
    [int]$Seed = 1425,
    [ValidateRange(2, 65536)][int]$MaximumQueue = 8192,
    [ValidateRange(1, 3600)][int]$StatisticsSeconds = 10,
    [ValidateSet('A', 'B', 'C')][string]$MachineLabel = 'B',
    [string]$RunRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

[System.Net.IPAddress]$serverIp = $null
[System.Net.IPAddress]$listenIp = $null
if (-not [System.Net.IPAddress]::TryParse($ServerAddress, [ref]$serverIp)) {
    throw 'ServerAddress must be a numeric unicast IPv4 or IPv6 address for deterministic proxy routing.'
}
if (-not [System.Net.IPAddress]::TryParse($ListenAddress, [ref]$listenIp)) {
    throw 'ListenAddress must be a numeric local IP address or wildcard address.'
}
if ($JitterMilliseconds -gt $DelayMilliseconds) {
    throw 'JitterMilliseconds cannot exceed DelayMilliseconds.'
}
if ($ReorderEvery -eq 1) {
    throw 'ReorderEvery must be zero (disabled) or at least two.'
}
$serverEndpointText = if ($serverIp.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) { "[$ServerAddress]:$ServerPort" } else { "$ServerAddress`:$ServerPort" }
$listenEndpointText = if ($listenIp.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) { "[$ListenAddress]:$ListenPort" } else { "$ListenAddress`:$ListenPort" }

$packageRoot = Get-ScannerPackageRoot
$info = Get-ScannerBuildInfo -PackageRoot $packageRoot
Set-ScannerBundledRuntime -PackageRoot $packageRoot
$dotnetHost = Join-Path $packageRoot 'runtime\dotnet.exe'
$proxyDll = Join-Path $packageRoot 'proxy\ScannerRoom.NetworkImpairmentProxy.dll'
if (-not (Test-Path -LiteralPath $proxyDll -PathType Leaf)) {
    throw "Packaged network impairment proxy is missing: $proxyDll"
}

$runsBase = Get-ScannerRunRoot -PackageId $info.PackageId -Override $RunRoot
$proxyName = 'proxy-' + $MachineLabel.ToLowerInvariant()
$proxyData = Assert-ScannerChildPath -Path (Join-Path $runsBase $proxyName) -Parent $runsBase
$logDirectory = Join-Path $proxyData 'logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$runStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfffZ')
$logPath = Join-Path $logDirectory "network-impairment-$runStamp.log"

$runInfo = [ordered]@{
    PackageId = $info.PackageId
    Role = $proxyName
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    DataPath = $proxyData
    ListenEndpoint = $listenEndpointText
    ServerEndpoint = $serverEndpointText
    DelayMilliseconds = $DelayMilliseconds
    JitterMilliseconds = $JitterMilliseconds
    LossPercent = $LossPercent.ToString('0.00', [System.Globalization.CultureInfo]::InvariantCulture)
    ReorderEvery = $ReorderEvery
    ReorderHoldMilliseconds = $ReorderHoldMilliseconds
    Seed = $Seed
    MaximumQueue = $MaximumQueue
    StatisticsSeconds = $StatisticsSeconds
    LogPath = $logPath
}
$runInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $proxyData 'scanner-run-info.json') -Encoding UTF8

$arguments = @(
    $proxyDll,
    '--listen', $listenEndpointText,
    '--server', $serverEndpointText,
    '--delay-ms', $DelayMilliseconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--jitter-ms', $JitterMilliseconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--loss-percent', $LossPercent.ToString('0.00', [System.Globalization.CultureInfo]::InvariantCulture),
    '--reorder-every', $ReorderEvery.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--reorder-hold-ms', $ReorderHoldMilliseconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--seed', $Seed.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--max-queue', $MaximumQueue.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--stats-seconds', $StatisticsSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
)

Write-Host "Package: $($info.PackageId)"
Write-Host "Proxy log: $logPath"
Write-Host "Impaired client endpoint: <this-machine-ip>`:$ListenPort"
Write-Host "Real server endpoint: $serverEndpointText"
Write-Host 'Only one impaired client may use this proxy. Press Ctrl+C once and wait for the final [NIP1] stop line.'

& $dotnetHost @arguments 2>&1 | Tee-Object -FilePath $logPath
$proxyExitCode = $LASTEXITCODE
if ($proxyExitCode -ne 0) {
    throw "Network impairment proxy exited with code $proxyExitCode."
}
return
