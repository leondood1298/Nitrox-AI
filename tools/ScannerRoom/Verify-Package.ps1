[CmdletBinding()]
param(
    [string]$PackageRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$TrustedManifestPath,
    [switch]$SkipRuntimeSmoke,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

$packagePath = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$packageManifestPath = Join-Path $packagePath 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
    throw "Package hash manifest is missing: $packageManifestPath"
}
if ([string]::IsNullOrWhiteSpace($TrustedManifestPath)) {
    $TrustedManifestPath = $packageManifestPath
}
$trustedManifest = [System.IO.Path]::GetFullPath($TrustedManifestPath)
if (-not (Test-Path -LiteralPath $trustedManifest -PathType Leaf)) {
    throw "Trusted package hash manifest is missing: $trustedManifest"
}

$failures = [System.Collections.Generic.List[string]]::new()
if (-not $trustedManifest.Equals($packageManifestPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    $trustedManifestHash = (Get-FileHash -LiteralPath $trustedManifest -Algorithm SHA256).Hash
    $packageManifestHash = (Get-FileHash -LiteralPath $packageManifestPath -Algorithm SHA256).Hash
    if (-not $trustedManifestHash.Equals($packageManifestHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add('Installed SHA256SUMS.txt does not match the verified source manifest.')
    }
}

$expectedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$expectedHashes = @{}
foreach ($line in Get-Content -LiteralPath $trustedManifest) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
        continue
    }
    if ($line -notmatch '^(?<hash>[A-Fa-f0-9]{64}) \*(?<path>.+)$') {
        $failures.Add("Malformed manifest line: $line")
        continue
    }

    $hash = $Matches.hash
    $relativeManifestPath = $Matches.path.Replace('\', '/')
    if (-not $expectedPaths.Add($relativeManifestPath)) {
        $failures.Add("Duplicate manifest path: $relativeManifestPath")
        continue
    }
    $expectedHashes[$relativeManifestPath] = $hash

    $relativeWindowsPath = $relativeManifestPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $packagePath $relativeWindowsPath))
    $prefix = $packagePath + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add("Manifest path escapes package root: $relativeManifestPath")
        continue
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        $failures.Add("Missing: $relativeManifestPath")
        continue
    }

    $actualHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    if (-not $actualHash.Equals($hash, [System.StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add("Hash mismatch: $relativeManifestPath")
    }
}

if ($expectedPaths.Count -eq 0) {
    $failures.Add('Manifest contained no files.')
}

foreach ($file in Get-ChildItem -LiteralPath $packagePath -Recurse -File) {
    $relative = $file.FullName.Substring($packagePath.Length + 1).Replace('\', '/')
    if ($relative.Equals('SHA256SUMS.txt', [System.StringComparison]::OrdinalIgnoreCase)) {
        continue
    }
    $isEvidenceSidecar = $relative -match '(?i)^evidence-inbox/[^/]+\.zip(?:\.sha256|\.summary\.txt|\.summary\.json)?$'
    if ($isEvidenceSidecar) {
        continue
    }
    if (-not $expectedPaths.Contains($relative)) {
        $failures.Add("Unexpected unmanifested file: $relative")
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures | Select-Object -First 50) {
        Write-Error -Message $failure -ErrorAction Continue
    }
    if ($failures.Count -gt 50) {
        Write-Error -Message "Additional verification problems omitted from console output: $($failures.Count - 50)" -ErrorAction Continue
    }
    throw "Package verification failed with $($failures.Count) problem(s)."
}

if (-not $SkipRuntimeSmoke) {
    $runtimeConfigs = @(
        (Join-Path $packagePath 'app\Nitrox.Launcher.runtimeconfig.json'),
        (Join-Path $packagePath 'app\Nitrox.Server.Subnautica.runtimeconfig.json'),
        (Join-Path $packagePath 'proxy\ScannerRoom.NetworkImpairmentProxy.runtimeconfig.json')
    )
    [void](Assert-ScannerRuntimeBundle -PackageRoot $packagePath -RuntimeConfigPaths $runtimeConfigs)
    Set-ScannerBundledRuntime -PackageRoot $packagePath
    $dotnetHost = Join-Path $packagePath 'runtime\dotnet.exe'
    $runtimeList = @(& $dotnetHost --list-runtimes 2>&1)
    if ($LASTEXITCODE -ne 0 -or $runtimeList.Count -eq 0) {
        throw 'Bundled dotnet host failed to enumerate runtimes.'
    }

    $proxyDll = Join-Path $packagePath 'proxy\ScannerRoom.NetworkImpairmentProxy.dll'
    $selfTestOutput = @(& $dotnetHost $proxyDll --self-test 2>&1)
    if ($LASTEXITCODE -ne 0 -or -not ($selfTestOutput -match '^\[SELFTEST\] PASS')) {
        $details = ($selfTestOutput | Select-Object -Last 20) -join [Environment]::NewLine
        throw "Packaged network impairment proxy self-test failed.`r`n$details"
    }
}

if (-not $Quiet) {
    Write-Host "PASS: verified $($expectedPaths.Count) packaged files under $packagePath"
    if (-not $SkipRuntimeSmoke) {
        Write-Host 'PASS: bundled runtime and network impairment proxy self-test'
    }
}
