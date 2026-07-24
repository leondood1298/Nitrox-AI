[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param($Expected, $Actual, [Parameter(Mandatory = $true)][string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Text.IndexOf($Value, [System.StringComparison]::Ordinal) -lt 0) {
        throw "$Message Missing '$Value'."
    }
}

$scannerToolRoot = Split-Path -Parent $PSScriptRoot
$summarizer = Join-Path $scannerToolRoot 'Summarize-ScannerLogs.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Nitrox-ScannerSummaryTests-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    $mixedInput = Join-Path $testRoot 'mixed-input'
    $mixedOutput = Join-Path $testRoot 'mixed-output'
    New-Item -ItemType Directory -Path $mixedInput -Force | Out-Null
    [System.IO.File]::WriteAllLines((Join-Path $mixedInput 'game.log'), @(
        '[SRD1] n=1 ep=01020304 side=C ev=dock out=ok room=11111111 fp=- reason=fixture',
        '[BPD1] n=1 ep=a1b2c3d4 side=C ev=audio_down out=suppress base=11111111.1111 source=- power=0.00/2500.00 initial=0 wait=1 reason=initial_sync',
        '[BPD1] n=1 ep=a1b2c3d4 side=C ev=audio_down out=suppress base=11111111.1111 source=- power=0.00/2500.00 initial=0 wait=1 reason=initial_sync',
        '[BPD1] n=2 ep=a1b2c3d4 side=C ev=audio_up out=pass base=11111111.1111 source=- power=2500.00/2500.00 initial=1 wait=0 reason=live',
        '[BPD1] n=3 ep=a1b2c3d4 side=C ev=source_apply out=ok base=- source=22222222.2222 power=125.00/250.00 initial=0 wait=1 reason=metadata_thermal_17',
        '[BPD1] n=4 ep=a1b2c3d4 side=C ev=source_apply out=missing base=- source=33333333.3333 power=0.00/75.00 initial=0 wait=1 reason=packet_solar_4',
        '[BPD1] n=5 ep=a1b2c3d4 side=C ev=audio_trace_limit out=truncated base=- source=- power=0.00/0.00 initial=1 wait=0 reason=capacity_16',
        '[BPD1] n=1 ep=b1c2d3e4 side=C ev=audio_down out=suppress base=11111111.1111 source=- power=0.00/2500.00 initial=0 wait=1 reason=initial_sync'
    ))

    & $summarizer -InputRoot $mixedInput -OutputDirectory $mixedOutput -MaxSummaryBytes 20480 -MaxTraceBytes 32768
    $mixedJson = Get-Content -LiteralPath (Join-Path $mixedOutput 'summary.json') -Raw | ConvertFrom-Json
    $mixedText = Get-Content -LiteralPath (Join-Path $mixedOutput 'scanner-summary.txt') -Raw
    $mixedTrace = Get-Content -LiteralPath (Join-Path $mixedOutput 'scanner-trace.log') -Raw

    Assert-Equal 'scanner-summary-v3' $mixedJson.Schema 'SRD schema changed.'
    Assert-Equal 1 $mixedJson.ScannerEventCount 'SRD event count changed.'
    Assert-Equal 1 $mixedJson.EventCounts.dock 'SRD event breakdown changed.'
    Assert-Equal 1 $mixedJson.OutcomeCounts.ok 'SRD outcome breakdown changed.'
    Assert-Equal 6 $mixedJson.BasePowerEventCount 'BPD unique count is wrong.'
    Assert-Equal 2 $mixedJson.BasePowerProcessEpochCount 'BPD epochs were collapsed.'
    Assert-Equal 1 $mixedJson.BasePowerRowsDeduplicated 'BPD duplicate count is wrong.'
    Assert-Equal 2 $mixedJson.BasePowerAudioSuppressCount 'BPD suppress count is wrong.'
    Assert-Equal 1 $mixedJson.BasePowerAudioPassCount 'BPD pass count is wrong.'
    Assert-Equal 1 $mixedJson.BasePowerSourceOkCount 'BPD source-ok count is wrong.'
    Assert-Equal 1 $mixedJson.BasePowerSourceMissingCount 'BPD source-missing count is wrong.'
    Assert-Equal 1 $mixedJson.BasePowerTraceTruncationCount 'BPD truncation count is wrong.'
    Assert-Equal 0 $mixedJson.ValidationIssueCount 'Valid mixed diagnostics produced validation issues.'
    Assert-Contains $mixedText 'BasePowerAudioSuppress=2' 'Text summary omitted BPD counts.'
    Assert-Contains $mixedTrace 'BASEPOWER-AUDIO-PASS|' 'Trace omitted live audio evidence.'
    Assert-Contains $mixedTrace 'BASEPOWER-SOURCE-MISSING|' 'Trace omitted missing-source evidence.'
    Assert-Contains $mixedTrace 'BASEPOWER-TRACE-TRUNCATION|' 'Trace omitted bounded-cap evidence.'

    $invalidInput = Join-Path $testRoot 'invalid-input'
    $invalidOutput = Join-Path $testRoot 'invalid-output'
    New-Item -ItemType Directory -Path $invalidInput -Force | Out-Null
    [System.IO.File]::WriteAllLines((Join-Path $invalidInput 'game.log'), @(
        '[BPD1] n=1 ep=1234abcd side=C ev=audio_down out=suppress base=- source=- power=0.00/1.00 initial=0 wait=1 reason=initial_sync',
        '[BPD1] n=1 ep=1234abcd side=C ev=audio_down out=pass base=- source=- power=0.00/1.00 initial=1 wait=0 reason=live',
        '[BPD1] n=x ep=bad side=S ev=source_apply out=missing base=- source=- power=0.00/1.00 initial=0 wait=1 reason=fixture',
        '[BPD1] n=2 side=C ev=source_apply out=ok base=- source=- power=1.00/1.00 initial=0 wait=1 reason=fixture'
    ))

    & $summarizer -InputRoot $invalidInput -OutputDirectory $invalidOutput -MaxSummaryBytes 20480 -MaxTraceBytes 16384
    $invalidJson = Get-Content -LiteralPath (Join-Path $invalidOutput 'summary.json') -Raw | ConvertFrom-Json
    $invalidIssues = @($invalidJson.ValidationIssues) -join "`n"
    Assert-Contains $invalidIssues 'bpd1-sequence-conflict' 'Conflicting BPD sequence was not isolated.'
    Assert-Contains $invalidIssues 'invalid-bpd1-sequence' 'Malformed BPD sequence was not isolated.'
    Assert-Contains $invalidIssues 'unparsed-bpd1' 'Missing BPD provenance was not isolated.'

    $stressInput = Join-Path $testRoot 'stress-input'
    $stressOutput = Join-Path $testRoot 'stress-output'
    New-Item -ItemType Directory -Path $stressInput -Force | Out-Null
    $stressLines = [System.Collections.Generic.List[string]]::new()
    for ($sequence = 1; $sequence -le 400; $sequence++) {
        $stressLines.Add("[SRD1] n=$sequence ep=0badcafe side=C ev=fixture out=ok room=11111111 fp=0123456789abcdef reason=bounded_trace_stress")
    }
    for ($sequence = 1; $sequence -le 400; $sequence++) {
        $stressLines.Add("[BPD1] n=$sequence ep=feedbeef side=C ev=source_apply out=ok base=- source=22222222.2222 power=125.00/250.00 initial=0 wait=1 reason=metadata_thermal_17")
    }
    $stressLines.Add('[BPD1] n=401 ep=feedbeef side=C ev=source_apply out=missing base=- source=33333333.3333 power=0.00/75.00 initial=0 wait=1 reason=packet_solar_4')
    $stressLines.Add('[BPD1] n=402 ep=feedbeef side=C ev=source_trace_limit out=truncated base=- source=- power=0.00/0.00 initial=1 wait=0 reason=capacity_48')
    [System.IO.File]::WriteAllLines((Join-Path $stressInput 'game.log'), $stressLines)

    & $summarizer -InputRoot $stressInput -OutputDirectory $stressOutput -MaxSummaryBytes 4096 -MaxTraceBytes 16384
    $stressJson = Get-Content -LiteralPath (Join-Path $stressOutput 'summary.json') -Raw | ConvertFrom-Json
    $stressTrace = Get-Content -LiteralPath (Join-Path $stressOutput 'scanner-trace.log') -Raw
    Assert-Equal 1 $stressJson.BasePowerSourceMissingCount 'Minimum-size JSON lost the missing-source count.'
    Assert-Equal 1 $stressJson.BasePowerTraceTruncationCount 'Minimum-size JSON lost the truncation count.'
    Assert-Contains $stressTrace 'BASEPOWER-SOURCE-MISSING|' 'Bounded trace lost newest missing-source evidence.'
    Assert-Contains $stressTrace 'BASEPOWER-TRACE-TRUNCATION|' 'Bounded trace lost newest truncation evidence.'
    Assert-Contains $stressTrace 'TRUNCATED omitted=' 'Trace stress did not exercise the byte cap.'

    Write-Host 'Summarize-ScannerLogs BPD1 tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot -PathType Container) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
