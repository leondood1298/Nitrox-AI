[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputRoot,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [ValidateRange(4096, 102400)][int]$MaxSummaryBytes = 20480,
    [ValidateRange(16384, 1048576)][int]$MaxTraceBytes = 262144
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

function Get-CompactFields {
    param([Parameter(Mandatory = $true)][string]$Text)

    $fields = @{}
    foreach ($match in [regex]::Matches($Text, '(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>[^\s|]+)')) {
        $fields[$match.Groups['key'].Value] = $match.Groups['value'].Value
    }
    return $fields
}

function Add-BoundedTextLine {
    param(
        # Windows PowerShell 5.1 enumerates a generic list during parameter
        # conversion. If that list already contains a deliberate blank line,
        # a strongly typed List[string] parameter rejects the whole argument.
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][object]$Lines,
        [Parameter(Mandatory = $true)][hashtable]$State,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Line,
        [Parameter(Mandatory = $true)][int]$Limit,
        [Parameter(Mandatory = $true)][System.Text.Encoding]$Encoding
    )

    if (-not ($Lines -is [System.Collections.Generic.List[string]])) {
        throw 'Add-BoundedTextLine requires a List[string] accumulator.'
    }

    $safeLine = if ($Line.Length -gt 1200) { $Line.Substring(0, 1200) + ' ...<line-truncated>' } else { $Line }
    $lineBytes = $Encoding.GetByteCount($safeLine + [Environment]::NewLine)
    if (($State.Bytes + $lineBytes) -le $Limit) {
        $Lines.Add($safeLine)
        $State.Bytes += $lineBytes
        return $true
    }
    $State.Omitted++
    return $false
}

function Write-BoundedTextLines {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Lines,
        [Parameter(Mandatory = $true)][int]$MaximumBytes,
        [Parameter(Mandatory = $true)][System.Text.Encoding]$Encoding
    )

    $selected = [System.Collections.Generic.List[string]]::new()
    $state = @{ Bytes = 0; Omitted = 0 }
    $contentLimit = [Math]::Max(1024, $MaximumBytes - 256)
    foreach ($line in $Lines) {
        [void](Add-BoundedTextLine -Lines $selected -State $state -Line $line -Limit $contentLimit -Encoding $Encoding)
    }
    if ($state.Omitted -gt 0) {
        [void](Add-BoundedTextLine -Lines $selected -State $state -Line "TRUNCATED omitted=$($state.Omitted)" -Limit $MaximumBytes -Encoding $Encoding)
    }
    [System.IO.File]::WriteAllText($Path, ([string]::Join([Environment]::NewLine, $selected) + [Environment]::NewLine), $Encoding)
    if ((Get-Item -LiteralPath $Path).Length -gt $MaximumBytes) {
        throw "Bounded text output exceeded $MaximumBytes bytes: $Path"
    }
}

$inputPath = [System.IO.Path]::GetFullPath($InputRoot)
if (-not (Test-Path -LiteralPath $inputPath -PathType Container)) {
    throw "Diagnostic input path does not exist: $inputPath"
}
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$scannerByKey = @{}
$scannerSnapshots = [System.Collections.Generic.List[object]]::new()
$snapshotTexts = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$testRows = [System.Collections.Generic.List[object]]::new()
$nipRows = [System.Collections.Generic.List[object]]::new()
$problemRows = [System.Collections.Generic.List[object]]::new()
$validationIssues = [System.Collections.Generic.List[string]]::new()
$legacyEpochState = @{}
$duplicateScannerRows = 0
$legacyScannerRows = 0
$legacyEpochResets = 0
$ordinal = 0L
$logFiles = @(Get-ChildItem -LiteralPath $inputPath -Recurse -File -Filter '*.log' -ErrorAction SilentlyContinue | Sort-Object FullName)

foreach ($file in $logFiles) {
    $relative = $file.FullName.Substring($inputPath.Length).TrimStart('\', '/')
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        $ordinal++
        $scannerOut = $null
        $scannerIndex = $line.IndexOf('[SRD1]', [System.StringComparison]::Ordinal)
        if ($scannerIndex -ge 0) {
            $scannerText = (Protect-ScannerEvidenceText -Text $line.Substring($scannerIndex)).Trim()
            $fields = Get-CompactFields -Text $scannerText
            if ($fields.ContainsKey('n') -and $fields.ContainsKey('side')) {
                $sequence = 0L
                if (-not [long]::TryParse([string]$fields.n, [ref]$sequence)) {
                    $validationIssues.Add("invalid-sequence source=$relative line=$lineNumber value=$($fields.n)")
                }
                else {
                    $side = [string]$fields.side
                    if ($fields.ContainsKey('ep')) {
                        $epoch = ([string]$fields.ep).ToLowerInvariant()
                        if ($epoch -notmatch '^[0-9a-f]{8}$') {
                            $validationIssues.Add("invalid-epoch source=$relative line=$lineNumber value=$($fields.ep)")
                        }
                    }
                    else {
                        $legacyScannerRows++
                        $legacyStateKey = "$relative|$side"
                        if (-not $legacyEpochState.ContainsKey($legacyStateKey)) {
                            $legacyEpochState[$legacyStateKey] = @{ Index = 0; MaximumSequence = 0L }
                        }
                        $legacyState = $legacyEpochState[$legacyStateKey]
                        $epoch = "legacy$($legacyState.Index)"

                        # Legacy rows predate ep=. If a single appended log
                        # contains a conflicting n=1 after a higher sequence,
                        # preserve both process runs under synthetic epochs.
                        # Exact history reprints remain ordinary duplicates.
                        $legacyCandidateKey = "$relative|$side|$epoch|$sequence"
                        if ($sequence -eq 1 -and $legacyState.MaximumSequence -gt 1 -and
                            $scannerByKey.ContainsKey($legacyCandidateKey) -and
                            -not $scannerByKey[$legacyCandidateKey].Text.Equals($scannerText, [System.StringComparison]::Ordinal)) {
                            $legacyState.Index++
                            $legacyState.MaximumSequence = 0L
                            $legacyEpochResets++
                            $epoch = "legacy$($legacyState.Index)"
                        }
                        if ($sequence -gt $legacyState.MaximumSequence) {
                            $legacyState.MaximumSequence = $sequence
                        }
                    }

                    # ep= is process-local provenance. Source remains part of
                    # the key so copied/rolled logs cannot collapse each other.
                    $key = "$relative|$side|$epoch|$sequence"
                    $row = [pscustomobject]@{
                        Ordinal = $ordinal
                        Source = $relative
                        Line = $lineNumber
                        Sequence = $sequence
                        Side = $side
                        Epoch = $epoch
                        Event = if ($fields.ContainsKey('ev')) { [string]$fields.ev } else { '-' }
                        Outcome = if ($fields.ContainsKey('out')) { [string]$fields.out } else { '-' }
                        Room = if ($fields.ContainsKey('room')) { [string]$fields.room } else { '-' }
                        Fingerprint = if ($fields.ContainsKey('fp')) { [string]$fields.fp } else { '-' }
                        Reason = if ($fields.ContainsKey('reason')) { [string]$fields.reason } else { '-' }
                        Text = $scannerText
                    }
                    $scannerOut = $row.Outcome
                    if ($scannerByKey.ContainsKey($key)) {
                        $duplicateScannerRows++
                        if (-not $scannerByKey[$key].Text.Equals($scannerText, [System.StringComparison]::Ordinal)) {
                            $validationIssues.Add("sequence-conflict key=$key first=$($scannerByKey[$key].Source):$($scannerByKey[$key].Line) duplicate=$relative`:$lineNumber")
                        }
                    }
                    else {
                        $scannerByKey[$key] = $row
                    }
                }
            }
            elseif ($scannerText -match '^\[SRD1\]\s+snapshot\b') {
                if ($snapshotTexts.Add($scannerText)) {
                    $scannerSnapshots.Add([pscustomobject]@{ Ordinal = $ordinal; Source = $relative; Line = $lineNumber; Text = $scannerText })
                }
            }
            else {
                $validationIssues.Add("unparsed-srd1 source=$relative line=$lineNumber")
            }
        }

        $testIndex = $line.IndexOf('TEST|', [System.StringComparison]::Ordinal)
        if ($testIndex -ge 0) {
            $testRows.Add([pscustomobject]@{ Ordinal = $ordinal; Source = $relative; Line = $lineNumber; Text = (Protect-ScannerEvidenceText -Text $line.Substring($testIndex)).Trim() })
        }

        $nipIndex = $line.IndexOf('[NIP1]', [System.StringComparison]::Ordinal)
        if ($nipIndex -ge 0) {
            $nipRows.Add([pscustomobject]@{ Ordinal = $ordinal; Source = $relative; Line = $lineNumber; Text = (Protect-ScannerEvidenceText -Text $line.Substring($nipIndex)).Trim() })
        }

        $isScannerExpectedWarning = $scannerIndex -ge 0 -and $scannerOut -in @('reject', 'checkpoint', 'ok')
        if (-not $isScannerExpectedWarning -and $line -match '(?i)\b(wrn|warn|warning|erro|error|crit|fatal|fail(?:ed|ure)?|exception)\b') {
            $protectedLine = Protect-ScannerEvidenceText -Text $line
            $signature = $protectedLine -replace '[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}', '<id>'
            $signature = $signature -replace '(?i)(\b(?:room|cam|entity|owner)=)[0-9a-f]{8,}', '$1<id>'
            $signature = $signature -replace '\b\d{2,}\b', '<n>'
            $signature = $signature -replace '^.*?\b(WRN|WARN(?:ING)?|ERRO|ERROR|CRIT|FATAL|FAIL(?:ED|URE)?)\b\s*[:| -]*', '$1 '
            if ($signature.Length -gt 240) { $signature = $signature.Substring(0, 240) }
            $problemRows.Add([pscustomobject]@{ Ordinal = $ordinal; Source = $relative; Line = $lineNumber; Signature = $signature; Text = $protectedLine })
        }
    }
}

$scannerRows = @($scannerByKey.Values | Sort-Object Ordinal)
$serverSampledSequenceGaps = [System.Collections.Generic.List[object]]::new()
$clientSequenceGaps = [System.Collections.Generic.List[object]]::new()
foreach ($sideGroup in $scannerRows | Group-Object { "$($_.Source)|$($_.Side)|$($_.Epoch)" }) {
    $previous = $null
    foreach ($row in $sideGroup.Group | Sort-Object Sequence) {
        if ($null -ne $previous -and $row.Sequence -gt ($previous.Sequence + 1)) {
            $gap = [pscustomobject]@{ Source = $row.Source; Side = $row.Side; Epoch = $row.Epoch; After = $previous.Sequence; Next = $row.Sequence; Missing = $row.Sequence - $previous.Sequence - 1 }
            if ($row.Side -eq 'S') {
                # Server rejection diagnostics are deliberately sampled. A
                # sequence gap alone is therefore advisory, not evidence loss.
                $serverSampledSequenceGaps.Add($gap)
            }
            else {
                $clientSequenceGaps.Add($gap)
                $validationIssues.Add("client-sequence-gap source=$($row.Source) side=$($row.Side) ep=$($row.Epoch) after=$($previous.Sequence) next=$($row.Sequence)")
            }
        }
        $previous = $row
    }
}

$checkpointRows = @($scannerRows | Where-Object { $_.Outcome -eq 'checkpoint' })
foreach ($checkpoint in $checkpointRows) {
    if ([string]::IsNullOrWhiteSpace($checkpoint.Fingerprint) -or $checkpoint.Fingerprint -eq '-') {
        $validationIssues.Add("checkpoint-missing-fingerprint side=$($checkpoint.Side) ep=$($checkpoint.Epoch) n=$($checkpoint.Sequence) reason=$($checkpoint.Reason)")
    }
}
foreach ($checkpointGroup in $checkpointRows | Where-Object { $_.Event -eq 'manual' } | Group-Object { "$($_.Side)|$($_.Room)|$($_.Reason)" }) {
    $fingerprints = @($checkpointGroup.Group.Fingerprint | Where-Object { $_ -ne '-' } | Sort-Object -Unique)
    if ($fingerprints.Count -gt 1) {
        $epochs = @($checkpointGroup.Group.Epoch | Sort-Object -Unique) -join ','
        $validationIssues.Add("manual-checkpoint-fingerprint-conflict key=$($checkpointGroup.Name) epochs=$epochs values=$($fingerprints -join ',')")
    }
}

$eventCounts = @{}
$outcomeCounts = @{}
foreach ($row in $scannerRows) {
    $eventCounts[$row.Event] = 1 + [int]$eventCounts[$row.Event]
    $outcomeCounts[$row.Outcome] = 1 + [int]$outcomeCounts[$row.Outcome]
}
$problemGroups = @($problemRows | Group-Object Signature | Sort-Object -Property @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Name'; Descending = $false })
$scannerFailures = @($scannerRows | Where-Object { $_.Outcome -in @('reject', 'invariant', 'diverge', 'fail', 'error') })

$encoding = [System.Text.UTF8Encoding]::new($false)
$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryState = @{ Bytes = 0; Omitted = 0 }
$summaryLimit = [Math]::Max(3072, $MaxSummaryBytes - 256)
function Add-SummaryLine {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Line)
    [void](Add-BoundedTextLine -Lines $script:summaryLines -State $script:summaryState -Line $Line -Limit $script:summaryLimit -Encoding $script:encoding)
}

function Get-ShortSummaryText {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text,
        [ValidateRange(80, 600)][int]$MaximumLength = 280
    )

    if ($Text.Length -le $MaximumLength) { return $Text }
    return $Text.Substring(0, $MaximumLength) + ' ...<line-truncated>'
}

function Format-ScannerSummaryRow {
    param([Parameter(Mandatory = $true)]$Row)

    $text = "side=$($Row.Side) ep=$($Row.Epoch) n=$($Row.Sequence) ev=$($Row.Event) out=$($Row.Outcome) room=$($Row.Room) fp=$($Row.Fingerprint) reason=$($Row.Reason) source=$($Row.Source):$($Row.Line)"
    return Get-ShortSummaryText -Text $text -MaximumLength 220
}

$scannerEpochGroups = @($scannerRows | Group-Object { "$($_.Source)|$($_.Side)|$($_.Epoch)" })
$scannerEpochCount = $scannerEpochGroups.Count
Add-SummaryLine 'Scanner Room diagnostic summary v3'
Add-SummaryLine "GeneratedUtc=$([DateTime]::UtcNow.ToString('o'))"
Add-SummaryLine "LogFiles=$($logFiles.Count)"
Add-SummaryLine "ScannerEventsUnique=$($scannerRows.Count)"
Add-SummaryLine "ScannerEpochs=$scannerEpochCount"
Add-SummaryLine "ScannerRowsDeduplicated=$duplicateScannerRows"
Add-SummaryLine "LegacyScannerRows=$legacyScannerRows"
Add-SummaryLine "LegacyEpochResetsDetected=$legacyEpochResets"
Add-SummaryLine "ServerSampledSequenceGaps=$($serverSampledSequenceGaps.Count)"
Add-SummaryLine "ClientSequenceGaps=$($clientSequenceGaps.Count)"
Add-SummaryLine "ScannerSnapshotsUnique=$($scannerSnapshots.Count)"
Add-SummaryLine "TestMarkers=$($testRows.Count)"
Add-SummaryLine "NetworkImpairmentLines=$($nipRows.Count)"
Add-SummaryLine "WarningsOrErrors=$($problemRows.Count)"
Add-SummaryLine "ValidationIssues=$($validationIssues.Count)"
Add-SummaryLine ''
Add-SummaryLine 'Validation issues (first 1):'
foreach ($issue in $validationIssues | Select-Object -First 1) { Add-SummaryLine ('  ' + (Get-ShortSummaryText -Text $issue -MaximumLength 200)) }
if ($validationIssues.Count -gt 1) { Add-SummaryLine "  ... additional=$($validationIssues.Count - 1) (see summary.json/raw evidence)" }
Add-SummaryLine ''
Add-SummaryLine 'Sequence gap observations (newest per category):'
foreach ($gap in $serverSampledSequenceGaps | Select-Object -Last 1) { Add-SummaryLine "  server-sampled source=$($gap.Source) ep=$($gap.Epoch) after=$($gap.After) next=$($gap.Next) missing=$($gap.Missing)" }
foreach ($gap in $clientSequenceGaps | Select-Object -Last 1) { Add-SummaryLine "  client-validation source=$($gap.Source) ep=$($gap.Epoch) after=$($gap.After) next=$($gap.Next) missing=$($gap.Missing)" }
Add-SummaryLine ''
Add-SummaryLine 'Checkpoint fingerprints (newest 1):'
foreach ($row in $checkpointRows | Select-Object -Last 1) { Add-SummaryLine ('  ' + (Format-ScannerSummaryRow -Row $row)) }
Add-SummaryLine ''
Add-SummaryLine 'Failure/rejection Scanner events (newest 2):'
foreach ($row in $scannerFailures | Select-Object -Last 2) { Add-SummaryLine ('  ' + (Format-ScannerSummaryRow -Row $row)) }
Add-SummaryLine ''
Add-SummaryLine 'Scanner event tail (newest 3):'
foreach ($row in $scannerRows | Select-Object -Last 3) { Add-SummaryLine ('  ' + (Format-ScannerSummaryRow -Row $row)) }
Add-SummaryLine ''
Add-SummaryLine 'TEST markers (newest 2):'
foreach ($row in $testRows | Select-Object -Last 2) { Add-SummaryLine ("  $($row.Source):$($row.Line) " + (Get-ShortSummaryText -Text $row.Text -MaximumLength 200)) }
Add-SummaryLine ''
Add-SummaryLine 'Network impairment profile/tail (start plus newest 1):'
$nipSelected = @($nipRows | Where-Object { $_.Text -match '\bev=start\b' } | Select-Object -First 1)
$nipSelected += @($nipRows | Select-Object -Last 1)
foreach ($row in $nipSelected | Sort-Object Ordinal -Unique) { Add-SummaryLine ("  $($row.Source):$($row.Line) " + (Get-ShortSummaryText -Text $row.Text -MaximumLength 200)) }
Add-SummaryLine ''
Add-SummaryLine 'Warning/error signatures (top 1):'
foreach ($group in $problemGroups | Select-Object -First 1) { Add-SummaryLine ("  count=$($group.Count) " + (Get-ShortSummaryText -Text $group.Name -MaximumLength 200)) }
Add-SummaryLine ''
Add-SummaryLine 'Event counts (first 20):'
foreach ($key in @($eventCounts.Keys | Sort-Object | Select-Object -First 20)) { Add-SummaryLine "  $key=$($eventCounts[$key])" }
if ($eventCounts.Count -gt 20) { Add-SummaryLine "  ... additional=$($eventCounts.Count - 20) (see summary.json)" }
Add-SummaryLine 'Outcome counts (first 10):'
foreach ($key in @($outcomeCounts.Keys | Sort-Object | Select-Object -First 10)) { Add-SummaryLine "  $key=$($outcomeCounts[$key])" }

if ($summaryState.Omitted -gt 0) {
    $truncationLine = "TRUNCATED omitted-summary-lines=$($summaryState.Omitted); full bounded trace and raw evidence remain in the ZIP."
    [void](Add-BoundedTextLine -Lines $summaryLines -State $summaryState -Line $truncationLine -Limit $MaxSummaryBytes -Encoding $encoding)
}
$summaryPath = Join-Path $outputPath 'scanner-summary.txt'
[System.IO.File]::WriteAllText($summaryPath, ([string]::Join([Environment]::NewLine, $summaryLines) + [Environment]::NewLine), $encoding)
if ((Get-Item -LiteralPath $summaryPath).Length -gt $MaxSummaryBytes) {
    throw "Scanner summary exceeded $MaxSummaryBytes bytes."
}

$traceLines = [System.Collections.Generic.List[string]]::new()
$traceLines.Add("TRACE v3 scanner=$($scannerRows.Count) epochs=$scannerEpochCount snapshots=$($scannerSnapshots.Count) test=$($testRows.Count) nip=$($nipRows.Count)")
$traceScannerKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
function Add-ScannerTraceRow {
    param(
        [Parameter(Mandatory = $true)]$Row,
        [Parameter(Mandatory = $true)][string]$Kind
    )

    $key = "$($Row.Source)|$($Row.Side)|$($Row.Epoch)|$($Row.Sequence)"
    if ($script:traceScannerKeys.Add($key)) {
        $script:traceLines.Add("$Kind|$($Row.Source):$($Row.Line)|$(Get-ShortSummaryText -Text $Row.Text -MaximumLength 300)")
    }
}

# Put a small, bounded sample of every high-value category first. The
# remaining Scanner tail is newest-first so a byte cap never preserves old
# rows at the expense of the event nearest the failure.
foreach ($row in $scannerFailures | Select-Object -Last 4) { Add-ScannerTraceRow -Row $row -Kind 'FAILURE' }
foreach ($row in $scannerRows | Select-Object -Last 8) { Add-ScannerTraceRow -Row $row -Kind 'SCANNER' }
foreach ($row in $testRows | Select-Object -Last 4) { $traceLines.Add("MARK|$($row.Source):$($row.Line)|$(Get-ShortSummaryText -Text $row.Text -MaximumLength 300)") }
$traceNipRows = @($nipRows | Where-Object { $_.Text -match '\bev=start\b' } | Select-Object -First 1)
$traceNipRows += @($nipRows | Select-Object -Last 4)
foreach ($row in $traceNipRows | Sort-Object Ordinal -Unique) { $traceLines.Add("NETWORK|$($row.Source):$($row.Line)|$(Get-ShortSummaryText -Text $row.Text -MaximumLength 300)") }
foreach ($row in $scannerSnapshots | Select-Object -Last 4) { $traceLines.Add("SNAPSHOT|$($row.Source):$($row.Line)|$(Get-ShortSummaryText -Text $row.Text -MaximumLength 300)") }
foreach ($row in $scannerRows | Sort-Object Ordinal -Descending | Select-Object -First 512) { Add-ScannerTraceRow -Row $row -Kind 'SCANNER-NEWEST-FIRST' }
Write-BoundedTextLines -Path (Join-Path $outputPath 'scanner-trace.log') -Lines $traceLines.ToArray() -MaximumBytes $MaxTraceBytes -Encoding $encoding

$jsonEventCounts = [ordered]@{}
foreach ($key in @($eventCounts.Keys | Sort-Object | Select-Object -First 40)) { $jsonEventCounts[$key] = $eventCounts[$key] }
$jsonOutcomeCounts = [ordered]@{}
foreach ($key in @($outcomeCounts.Keys | Sort-Object | Select-Object -First 20)) { $jsonOutcomeCounts[$key] = $outcomeCounts[$key] }
$json = [ordered]@{
    Schema = 'scanner-summary-v3'
    GeneratedUtc = [DateTime]::UtcNow.ToString('o')
    LogFileCount = $logFiles.Count
    ScannerEventCount = $scannerRows.Count
    ScannerProcessEpochCount = $scannerEpochCount
    ScannerRowsDeduplicated = $duplicateScannerRows
    LegacyScannerRowCount = $legacyScannerRows
    LegacyEpochResetCount = $legacyEpochResets
    ServerSampledSequenceGapCount = $serverSampledSequenceGaps.Count
    ClientSequenceGapCount = $clientSequenceGaps.Count
    ScannerFailureCount = $scannerFailures.Count
    ScannerSnapshotCount = $scannerSnapshots.Count
    TestMarkerCount = $testRows.Count
    NetworkImpairmentLineCount = $nipRows.Count
    WarningOrErrorCount = $problemRows.Count
    ValidationIssueCount = $validationIssues.Count
    EventCounts = $jsonEventCounts
    OutcomeCounts = $jsonOutcomeCounts
    ScannerEpochs = @($scannerEpochGroups | Select-Object -Last 20 | ForEach-Object { $epochRow = $_.Group[0]; [ordered]@{ Source = $epochRow.Source; Side = $epochRow.Side; Epoch = $epochRow.Epoch; FirstSequence = ($_.Group.Sequence | Measure-Object -Minimum).Minimum; LastSequence = ($_.Group.Sequence | Measure-Object -Maximum).Maximum; EventCount = $_.Count } })
    ServerSampledSequenceGaps = @($serverSampledSequenceGaps | Select-Object -Last 5)
    ClientSequenceGaps = @($clientSequenceGaps | Select-Object -Last 5)
    ValidationIssues = @($validationIssues | Select-Object -First 20)
    Checkpoints = @($checkpointRows | Select-Object -Last 20 | ForEach-Object { [ordered]@{ Source = $_.Source; Line = $_.Line; Side = $_.Side; Epoch = $_.Epoch; Sequence = $_.Sequence; Room = $_.Room; Fingerprint = $_.Fingerprint; Reason = $_.Reason } })
    ScannerFailures = @($scannerFailures | Select-Object -Last 5 | ForEach-Object { [ordered]@{ Source = $_.Source; Line = $_.Line; Side = $_.Side; Epoch = $_.Epoch; Sequence = $_.Sequence; Event = $_.Event; Outcome = $_.Outcome; Room = $_.Room; Fingerprint = $_.Fingerprint; Reason = $_.Reason } })
    TestMarkers = @($testRows | Select-Object -Last 5 | ForEach-Object { [ordered]@{ Source = $_.Source; Line = $_.Line; Text = (Get-ShortSummaryText -Text $_.Text -MaximumLength 400) } })
    NetworkImpairment = @($nipRows | Select-Object -Last 5 | ForEach-Object { [ordered]@{ Source = $_.Source; Line = $_.Line; Text = (Get-ShortSummaryText -Text $_.Text -MaximumLength 400) } })
    ProblemSignatures = @($problemGroups | Select-Object -First 20 | ForEach-Object { [ordered]@{ Count = $_.Count; Signature = $_.Name } })
    SummaryLinesOmitted = $summaryState.Omitted
}
$jsonText = $json | ConvertTo-Json -Depth 6 -Compress
if ($encoding.GetByteCount($jsonText + [Environment]::NewLine) -gt $MaxSummaryBytes) {
    $json['ValidationIssues'] = @($json['ValidationIssues'] | Select-Object -First 5)
    $json['ScannerEpochs'] = @($json['ScannerEpochs'] | Select-Object -Last 5)
    $json['ServerSampledSequenceGaps'] = @($json['ServerSampledSequenceGaps'] | Select-Object -Last 2)
    $json['ClientSequenceGaps'] = @($json['ClientSequenceGaps'] | Select-Object -Last 2)
    $json['Checkpoints'] = @($json['Checkpoints'] | Select-Object -Last 5)
    $json['ScannerFailures'] = @($json['ScannerFailures'] | Select-Object -Last 1)
    $json['TestMarkers'] = @($json['TestMarkers'] | Select-Object -Last 1)
    $json['NetworkImpairment'] = @($json['NetworkImpairment'] | Select-Object -Last 1)
    $json['ProblemSignatures'] = @($json['ProblemSignatures'] | Select-Object -First 5)
    $jsonText = $json | ConvertTo-Json -Depth 6 -Compress
}
if ($encoding.GetByteCount($jsonText + [Environment]::NewLine) -gt $MaxSummaryBytes) {
    $smallEventCounts = [ordered]@{}
    foreach ($key in @($eventCounts.Keys | Sort-Object | Select-Object -First 10)) { $smallEventCounts[$key] = $eventCounts[$key] }
    $smallOutcomeCounts = [ordered]@{}
    foreach ($key in @($outcomeCounts.Keys | Sort-Object | Select-Object -First 5)) { $smallOutcomeCounts[$key] = $outcomeCounts[$key] }
    $json['EventCounts'] = $smallEventCounts
    $json['OutcomeCounts'] = $smallOutcomeCounts
    $json['ValidationIssues'] = @($json['ValidationIssues'] | Select-Object -First 2)
    $json['ScannerEpochs'] = @($json['ScannerEpochs'] | Select-Object -Last 2)
    $json['ServerSampledSequenceGaps'] = @($json['ServerSampledSequenceGaps'] | Select-Object -Last 1)
    $json['ClientSequenceGaps'] = @($json['ClientSequenceGaps'] | Select-Object -Last 1)
    $json['Checkpoints'] = @($json['Checkpoints'] | Select-Object -Last 2)
    $json['ProblemSignatures'] = @($json['ProblemSignatures'] | Select-Object -First 2)
    $json['DetailTruncated'] = $true
    $jsonText = $json | ConvertTo-Json -Depth 6 -Compress
}
if ($encoding.GetByteCount($jsonText + [Environment]::NewLine) -gt $MaxSummaryBytes) {
    $json = [ordered]@{
        Schema = 'scanner-summary-v3'
        GeneratedUtc = [DateTime]::UtcNow.ToString('o')
        DetailTruncated = $true
        LogFileCount = $logFiles.Count
        ScannerEventCount = $scannerRows.Count
        ScannerProcessEpochCount = $scannerEpochCount
        ScannerRowsDeduplicated = $duplicateScannerRows
        LegacyScannerRowCount = $legacyScannerRows
        LegacyEpochResetCount = $legacyEpochResets
        ServerSampledSequenceGapCount = $serverSampledSequenceGaps.Count
        ClientSequenceGapCount = $clientSequenceGaps.Count
        ScannerFailureCount = $scannerFailures.Count
        ScannerSnapshotCount = $scannerSnapshots.Count
        TestMarkerCount = $testRows.Count
        NetworkImpairmentLineCount = $nipRows.Count
        WarningOrErrorCount = $problemRows.Count
        ValidationIssueCount = $validationIssues.Count
        LatestEpoch = @($json['ScannerEpochs'] | Select-Object -Last 1)
        LatestFailure = @($json['ScannerFailures'] | Select-Object -Last 1)
        LatestTestMarker = @($json['TestMarkers'] | Select-Object -Last 1)
        LatestNetworkImpairment = @($json['NetworkImpairment'] | Select-Object -Last 1)
        FirstValidationIssue = @($validationIssues | Select-Object -First 1)
    }
    $jsonText = $json | ConvertTo-Json -Depth 6 -Compress
}
if ($encoding.GetByteCount($jsonText + [Environment]::NewLine) -gt $MaxSummaryBytes) {
    throw 'Minimal summary JSON could not be bounded to the requested byte limit.'
}
[System.IO.File]::WriteAllText((Join-Path $outputPath 'summary.json'), $jsonText + [Environment]::NewLine, $encoding)
