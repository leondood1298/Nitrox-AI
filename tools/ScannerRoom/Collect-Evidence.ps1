[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('server', 'client', 'proxy')][string]$Role,
    [Parameter(Mandatory = $true)][string]$MachineLabel,
    [Parameter(Mandatory = $true)][string]$DataPath,
    [string]$OutputInbox,
    [string]$FailureNote = '',
    [ValidateRange(16, 4096)][int]$WarnAboveMiB = 512
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

function Copy-ScannerRedactedTextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
    $sourceStream = [System.IO.FileStream]::new(
        $Source,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
    try {
        $reader = [System.IO.StreamReader]::new($sourceStream, $true)
        try {
            $writer = [System.IO.StreamWriter]::new($Destination, $false, [System.Text.UTF8Encoding]::new($false))
            try {
                while (($line = $reader.ReadLine()) -ne $null) {
                    $writer.WriteLine((Protect-ScannerEvidenceText -Text $line))
                }
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $sourceStream.Dispose()
    }
}

function Copy-ScannerSnapshotTree {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [switch]$RedactText
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relative = $file.FullName.Substring($Source.Length).TrimStart('\', '/')
        $target = Join-Path $Destination $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        if ($RedactText -and $file.Extension -in @('.log', '.txt', '.json', '.cfg', '.xml', '.md')) {
            Copy-ScannerRedactedTextFile -Source $file.FullName -Destination $target
        }
        else {
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
}

function Get-ScannerTreeBytes {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return 0L
    }
    $measure = Get-ChildItem -LiteralPath $Path -Recurse -File | Measure-Object -Property Length -Sum
    if ($null -eq $measure.Sum) {
        return 0L
    }
    return [long]$measure.Sum
}

$packageRoot = Get-ScannerPackageRoot
$info = Get-ScannerBuildInfo -PackageRoot $packageRoot
$dataRoot = [System.IO.Path]::GetFullPath($DataPath)
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
    throw "Run data path does not exist: $dataRoot"
}
$outputPath = Get-ScannerEvidenceInbox -PackageRoot $packageRoot -Override $OutputInbox
$dataBoundary = $dataRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$normalizedOutput = [System.IO.Path]::GetFullPath($outputPath).TrimEnd('\', '/')
if ($normalizedOutput.Equals($dataRoot.TrimEnd('\', '/'), [System.StringComparison]::OrdinalIgnoreCase) -or
    $normalizedOutput.StartsWith($dataBoundary, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The evidence inbox cannot be the run data directory or one of its children.'
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfffZ')
$machine = ConvertTo-SafeScannerLabel -Value $MachineLabel
$roleLabel = ConvertTo-SafeScannerLabel -Value $Role
$evidenceId = "$runId-$machine-$roleLabel-$($info.PackageId)"
$stagingBase = Join-Path ([System.IO.Path]::GetTempPath()) 'Nitrox-AI-Scanner-Evidence'
New-Item -ItemType Directory -Path $stagingBase -Force | Out-Null
$staging = Assert-ScannerChildPath -Path (Join-Path $stagingBase ([Guid]::NewGuid().ToString('N'))) -Parent $stagingBase
New-Item -ItemType Directory -Path $staging -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $packageRoot 'BUILD_INFO.json') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Destination (Join-Path $staging 'PACKAGE-SHA256SUMS.txt')
    if (Test-Path -LiteralPath (Join-Path $packageRoot 'TEST_STATUS.md')) {
        Copy-Item -LiteralPath (Join-Path $packageRoot 'TEST_STATUS.md') -Destination $staging
    }

    $raw = Join-Path $staging 'raw'
    New-Item -ItemType Directory -Path $raw -Force | Out-Null
    foreach ($folder in @('logs', 'crashes', 'screenshots')) {
        $sourceFolder = Join-Path $dataRoot $folder
        if (Test-Path -LiteralPath $sourceFolder -PathType Container) {
            Copy-ScannerSnapshotTree -Source $sourceFolder -Destination (Join-Path $raw $folder) -RedactText:($folder -in @('logs', 'crashes'))
        }
    }
    foreach ($fileName in @('scanner-run-info.json', 'scanner-test-marks.log', 'servers')) {
        $sourceFile = Join-Path $dataRoot $fileName
        if (Test-Path -LiteralPath $sourceFile -PathType Leaf) {
            Copy-ScannerRedactedTextFile -Source $sourceFile -Destination (Join-Path $raw $fileName)
        }
    }

    $saveSource = Join-Path $dataRoot 'saves'
    if (Test-Path -LiteralPath $saveSource -PathType Container) {
        $saveTarget = Join-Path $raw 'saves'
        New-Item -ItemType Directory -Path $saveTarget -Force | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath $saveSource -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/]backups[\\/]' }) {
            $relative = $file.FullName.Substring($saveSource.Length).TrimStart('\', '/')
            $target = Join-Path $saveTarget $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            if ($file.Name -eq 'server.cfg') {
                Copy-ScannerRedactedTextFile -Source $file.FullName -Destination $target
            }
            else {
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
    }

    # Summarize the frozen/redacted snapshot so every cited line exists in raw/ inside the same ZIP.
    & (Join-Path $PSScriptRoot 'Summarize-ScannerLogs.ps1') -InputRoot $raw -OutputDirectory (Join-Path $staging 'summary') -MaxSummaryBytes 20480

    $snapshotBytes = Get-ScannerTreeBytes -Path $raw
    $metadata = [ordered]@{
        Schema = 'scanner-evidence-v2'
        EvidenceId = $evidenceId
        PackageId = $info.PackageId
        CollectedUtc = [DateTime]::UtcNow.ToString('o')
        Machine = (Protect-ScannerEvidenceText -Text $MachineLabel)
        Role = $Role
        FailureNote = (Protect-ScannerEvidenceText -Text (($FailureNote -replace '[\r\n]+', ' ').Trim()))
        SourceSnapshotBytes = $snapshotBytes
        SourceSnapshotMiB = [Math]::Round($snapshotBytes / 1MB, 2)
        PrivacyNotice = 'Text logs/config metadata were pattern-redacted; binary crash and save data can still contain player/world identifiers.'
    }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staging 'EVIDENCE_INFO.json') -Encoding UTF8

    $uncompressedBytes = Get-ScannerTreeBytes -Path $staging
    if ($uncompressedBytes -gt ($WarnAboveMiB * 1MB)) {
        Write-Warning "Evidence snapshot is $([Math]::Round($uncompressedBytes / 1MB, 1)) MiB before compression. Google Drive synchronization may take time."
    }

    $manifestPath = Join-Path $staging 'MANIFEST.sha256'
    Get-ChildItem -LiteralPath $staging -Recurse -File | Where-Object { $_.FullName -ne $manifestPath } | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        '{0} *{1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    } | Set-Content -LiteralPath $manifestPath -Encoding ASCII

    $zipPath = Join-Path $outputPath "$evidenceId.zip"
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    "$zipHash *$([System.IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ASCII
    Copy-Item -LiteralPath (Join-Path $staging 'summary\scanner-summary.txt') -Destination "$zipPath.summary.txt" -Force
    Copy-Item -LiteralPath (Join-Path $staging 'summary\summary.json') -Destination "$zipPath.summary.json" -Force

    Write-Host "Evidence collected: $zipPath"
    Write-Host "SHA-256: $zipHash"
    Write-Host "Archive bytes: $((Get-Item -LiteralPath $zipPath).Length)"
    Write-Host "Read this bounded summary first: $zipPath.summary.txt"
    Write-Output $zipPath
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
