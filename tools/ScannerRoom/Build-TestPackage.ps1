[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [Parameter(Mandatory = $true)][string]$DestinationRoot,
    [Parameter(Mandatory = $true)][string]$TestStatusPath,
    [Parameter(Mandatory = $true)][string]$DotNetHost,
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Subnautica',
    [string]$FixturePath,
    [ValidateSet('Final', 'Development')][string]$Mode = 'Final',
    [string[]]$AllowDirtyPath = @(),
    [switch]$SkipBuild,
    [switch]$Replace
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_Package.Common.ps1')

function Get-ScannerTextHash {
    param([AllowEmptyString()][string]$Text)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Test-ScannerRelevantSourcePath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.Replace('\', '/').Trim('"')
    if ($normalized -match '^(?i)(test_results|graphify-out|cross-repo-graphify-out)/') {
        return $false
    }
    if ($normalized -match '(?i)(^|/)(bin|obj)/') {
        return $false
    }
    return $true
}

function ConvertTo-ScannerExactRepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim()) {
        throw '-AllowDirtyPath values must be non-empty exact repo-relative paths without surrounding whitespace.'
    }
    if ([System.IO.Path]::IsPathRooted($Value) -or [System.Management.Automation.WildcardPattern]::ContainsWildcardCharacters($Value)) {
        throw "-AllowDirtyPath does not accept rooted paths or wildcards: $Value"
    }
    $normalized = $Value.Replace('\', '/')
    if ($normalized.StartsWith('/') -or $normalized.EndsWith('/') -or $normalized -match '(^|/)\.{1,2}(/|$)' -or $normalized.Contains(':')) {
        throw "-AllowDirtyPath must be one exact file path under the repository: $Value"
    }

    $repoPath = [System.IO.Path]::GetFullPath($Repository).TrimEnd('\', '/')
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $repoPath $normalized.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
    $prefix = $repoPath + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "-AllowDirtyPath escapes the repository: $Value"
    }
    return $candidate.Substring($prefix.Length).Replace('\', '/')
}

function Get-ScannerRelevantDirtyPaths {
    param([Parameter(Mandatory = $true)][string]$Repository)

    $paths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $commands = @(
        @('diff', '--name-only', '--'),
        @('diff', '--cached', '--name-only', '--'),
        @('ls-files', '--others', '--exclude-standard')
    )
    foreach ($arguments in $commands) {
        $output = @(& git -c core.quotepath=false -C $Repository @arguments)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect source state with git $($arguments -join ' ')."
        }
        foreach ($path in $output) {
            if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-ScannerRelevantSourcePath -RelativePath $path)) {
                [void]$paths.Add($path.Replace('\', '/').Trim('"'))
            }
        }
    }
    return @($paths | Sort-Object)
}

function Get-ScannerPathSetFingerprint {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$RelativePaths
    )

    $records = foreach ($relative in $RelativePaths | Sort-Object) {
        $path = Join-Path $Root $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $hash = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '<deleted>' }
        "$relative|$hash"
    }
    return Get-ScannerTextHash -Text ($records -join "`n")
}

function Get-ScannerTreeFingerprint {
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $records = foreach ($file in Get-ChildItem -LiteralPath $rootPath -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
        "$relative|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
    }
    return Get-ScannerTextHash -Text ($records -join "`n")
}

function Invoke-ScannerNative {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$destinationBase = [System.IO.Path]::GetFullPath($DestinationRoot)
$testStatus = [System.IO.Path]::GetFullPath($TestStatusPath)
if (-not (Test-Path -LiteralPath $DotNetHost -PathType Leaf)) {
    throw "dotnet host was not found: $DotNetHost"
}
if (-not (Test-Path -LiteralPath $testStatus -PathType Leaf)) {
    throw "Qualification TEST_STATUS input was not found: $testStatus"
}
$testStatusText = Get-Content -LiteralPath $testStatus -Raw
if ([string]::IsNullOrWhiteSpace($testStatusText)) {
    throw 'Package qualification status must not be empty.'
}
$automatedDeclarations = [regex]::Matches($testStatusText, '(?im)^[ \t]*AUTOMATED_QUALIFICATION[ \t]*:.*$')
if ($automatedDeclarations.Count -ne 1) {
    throw 'TEST_STATUS must contain exactly one AUTOMATED_QUALIFICATION declaration.'
}
$automatedStatus = [regex]::Match($automatedDeclarations[0].Value, '^AUTOMATED_QUALIFICATION:[ \t]*(?<state>[A-Z_]+)[ \t]*\r?$')
if (-not $automatedStatus.Success -or $automatedStatus.Groups['state'].Value -ne 'PASS') {
    throw 'Final packaging requires the exact status line AUTOMATED_QUALIFICATION: PASS; FAIL, PENDING, prose matches, and malformed declarations are rejected.'
}
$manualDeclarations = [regex]::Matches($testStatusText, '(?im)^[ \t]*MANUAL_MATRIX[ \t]*:.*$')
if ($manualDeclarations.Count -gt 1) {
    throw 'TEST_STATUS may contain at most one MANUAL_MATRIX declaration.'
}
if ($manualDeclarations.Count -eq 1) {
    $manualStatus = [regex]::Match($manualDeclarations[0].Value, '^MANUAL_MATRIX:[ \t]*(?:NOT_RUN|PASS)[ \t]*\r?$')
    if (-not $manualStatus.Success) {
        throw 'MANUAL_MATRIX, when present, must be exactly NOT_RUN or PASS. NOT_RUN is expected before the user performs real-game testing.'
    }
}
if ($Mode -eq 'Final' -and $SkipBuild) {
    throw '-SkipBuild is forbidden in Final mode because final packages must use fresh outputs.'
}

$commit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-f0-9]{40}$') {
    throw 'Could not resolve the source commit.'
}
$shortCommit = $commit.Substring(0, 12)
$dirtyPaths = @(Get-ScannerRelevantDirtyPaths -Repository $repo)
$allowedDirtySet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($allowedPath in $AllowDirtyPath) {
    [void]$allowedDirtySet.Add((ConvertTo-ScannerExactRepoRelativePath -Repository $repo -Value $allowedPath))
}
$unknownAllowances = @($allowedDirtySet | Where-Object { $_ -notin $dirtyPaths } | Sort-Object)
if ($unknownAllowances.Count -gt 0) {
    throw "-AllowDirtyPath must name a currently dirty path exactly; unmatched value(s):`r`n  $($unknownAllowances -join "`r`n  ")"
}
$unapprovedDirtyPaths = @($dirtyPaths | Where-Object { -not $allowedDirtySet.Contains($_) } | Sort-Object)
if ($Mode -eq 'Final' -and $unapprovedDirtyPaths.Count -gt 0) {
    throw "Final packaging refuses relevant dirty/untracked source not explicitly allowed with -AllowDirtyPath:`r`n  $($unapprovedDirtyPaths -join "`r`n  ")"
}
$explicitlyAllowedDirtyPaths = @($allowedDirtySet | Sort-Object)
$sourceFingerprint = Get-ScannerPathSetFingerprint -Root $repo -RelativePaths $dirtyPaths
$testStatusHash = (Get-FileHash -LiteralPath $testStatus -Algorithm SHA256).Hash
$fixtureHash = if ([string]::IsNullOrWhiteSpace($FixturePath)) { 'none' } else {
    $fixtureRoot = [System.IO.Path]::GetFullPath($FixturePath)
    if (-not (Test-Path -LiteralPath $fixtureRoot -PathType Container)) {
        throw "Fixture path is invalid: $fixtureRoot"
    }
    Get-ScannerTreeFingerprint -Root $fixtureRoot
}
$qualificationHash = Get-ScannerTextHash -Text "$testStatusHash|$fixtureHash"
$createdUtc = [DateTime]::UtcNow
$packageId = "scanner-room-$shortCommit-q$($qualificationHash.Substring(0, 8).ToLowerInvariant())-$($createdUtc.ToString('yyyyMMddTHHmmssZ'))-win-x64"
$packageRoot = Assert-ScannerChildPath -Path (Join-Path $destinationBase $packageId) -Parent $destinationBase
$zipPath = Join-Path $destinationBase "Nitrox-AI-$packageId.zip"
if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
    if (-not $Replace) {
        throw "Package/archive already exists; use -Replace only after confirming these targets: $packageRoot and $zipPath"
    }
}

$gameBuild = Get-ScannerGameBuild -GamePath $GamePath
$dotNetInstallRoot = Split-Path -Parent $DotNetHost
$dotNetInstallBase = Split-Path -Parent $dotNetInstallRoot
$env:DOTNET_CLI_HOME = Join-Path $dotNetInstallBase 'cli-home'
if (Test-Path -LiteralPath (Join-Path $dotNetInstallBase 'nuget-packages') -PathType Container) {
    $env:NUGET_PACKAGES = Join-Path $dotNetInstallBase 'nuget-packages'
}
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$stagingBase = Join-Path ([System.IO.Path]::GetTempPath()) 'Nitrox-AI-Scanner-Packages'
New-Item -ItemType Directory -Path $stagingBase -Force | Out-Null
$staging = Assert-ScannerChildPath -Path (Join-Path $stagingBase ([Guid]::NewGuid().ToString('N'))) -Parent $stagingBase
$sourceClone = Join-Path $staging 'source'
$packageStaging = Join-Path $staging 'package'
$proxyPublish = Join-Path $staging 'proxy-publish'
$destinationCopyStarted = $false

try {
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    if (-not $SkipBuild) {
        Invoke-ScannerNative -FilePath 'git' -Arguments @('clone', '--shared', '--no-checkout', '--quiet', '--', $repo, $sourceClone) -FailureMessage 'Could not create clean source clone.'
        Invoke-ScannerNative -FilePath 'git' -Arguments @('-C', $sourceClone, 'checkout', '--quiet', '--detach', $commit) -FailureMessage 'Could not check out exact source commit.'
        $cloneCommit = (& git -C $sourceClone rev-parse HEAD).Trim()
        if (-not $cloneCommit.Equals($commit, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Clean source clone resolved $cloneCommit instead of $commit."
        }

        $launcherProject = Join-Path $sourceClone 'Nitrox.Launcher\Nitrox.Launcher.csproj'
        Invoke-ScannerNative -FilePath $DotNetHost -Arguments @('restore', $launcherProject, '-r', 'win-x64', '--nologo') -FailureMessage 'Release launcher restore failed.'
        Invoke-ScannerNative -FilePath $DotNetHost -Arguments @('build', $launcherProject, '-c', 'Release', '-r', 'win-x64', '--no-restore', '--no-incremental', '--nologo', '-m:1', '-p:UseAppHost=true') -FailureMessage 'Release launcher build failed.'

        $proxyProject = Join-Path $sourceClone 'tools\ScannerRoom\NetworkImpairmentProxy\NetworkImpairmentProxy.csproj'
        Invoke-ScannerNative -FilePath $DotNetHost -Arguments @('restore', $proxyProject, '-r', 'win-x64', '--nologo') -FailureMessage 'Network proxy restore failed.'
        Invoke-ScannerNative -FilePath $DotNetHost -Arguments @('publish', $proxyProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '--no-restore', '--nologo', '-o', $proxyPublish) -FailureMessage 'Network proxy publish failed.'
        $appSource = Join-Path $sourceClone 'Nitrox.Launcher\bin\Release\net10.0\win-x64'
    }
    else {
        $appSource = Join-Path $repo 'Nitrox.Launcher\bin\Release\net10.0\win-x64'
        $proxyPublish = Join-Path $repo 'tools\ScannerRoom\NetworkImpairmentProxy\bin\Release\net10.0\win-x64'
    }
    $contentSourceRoot = if ($SkipBuild) { $repo } else { $sourceClone }

    foreach ($required in @('Nitrox.Launcher.exe', 'Nitrox.Launcher.dll', 'Nitrox.Server.Subnautica.exe', 'Nitrox.Server.Subnautica.dll', 'Nitrox.Launcher.runtimeconfig.json', 'Nitrox.Server.Subnautica.runtimeconfig.json', 'lib', 'Resources')) {
        if (-not (Test-Path -LiteralPath (Join-Path $appSource $required))) {
            throw "Release output is incomplete; missing $required under $appSource"
        }
    }
    foreach ($required in @('ScannerRoom.NetworkImpairmentProxy.exe', 'ScannerRoom.NetworkImpairmentProxy.dll', 'ScannerRoom.NetworkImpairmentProxy.deps.json', 'ScannerRoom.NetworkImpairmentProxy.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $proxyPublish $required) -PathType Leaf)) {
            throw "Network proxy output is incomplete; missing $required under $proxyPublish"
        }
    }

    $embeddedVersions = [ordered]@{}
    foreach ($assemblyName in @('Nitrox.Launcher.dll', 'Nitrox.Server.Subnautica.dll')) {
        $assemblyPath = Join-Path $appSource $assemblyName
        $productVersion = (Get-Item -LiteralPath $assemblyPath).VersionInfo.ProductVersion
        $embeddedVersions[$assemblyName] = $productVersion
        if ([string]::IsNullOrWhiteSpace($productVersion) -or $productVersion.IndexOf($commit, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "$assemblyName does not embed source commit $commit (ProductVersion='$productVersion')."
        }
    }

    New-Item -ItemType Directory -Path $packageStaging -Force | Out-Null
    $appDestination = Join-Path $packageStaging 'app'
    New-Item -ItemType Directory -Path $appDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $appSource '*') -Destination $appDestination -Recurse -Force
    $proxyDestination = Join-Path $packageStaging 'proxy'
    New-Item -ItemType Directory -Path $proxyDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $proxyPublish '*') -Destination $proxyDestination -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $contentSourceRoot 'tools\ScannerRoom\NetworkImpairmentProxy\README.md') -Destination (Join-Path $proxyDestination 'README.md') -Force

    $runtimeConfigPaths = @(
        (Join-Path $appDestination 'Nitrox.Launcher.runtimeconfig.json'),
        (Join-Path $appDestination 'Nitrox.Server.Subnautica.runtimeconfig.json'),
        (Join-Path $proxyDestination 'ScannerRoom.NetworkImpairmentProxy.runtimeconfig.json')
    )
    $runtimeRequirements = Get-ScannerRuntimeRequirements -RuntimeConfigPaths $runtimeConfigPaths
    $runtimeDestination = Join-Path $packageStaging 'runtime'
    New-Item -ItemType Directory -Path $runtimeDestination -Force | Out-Null
    Copy-Item -LiteralPath $DotNetHost -Destination $runtimeDestination
    Copy-Item -LiteralPath (Join-Path $dotNetInstallRoot 'host') -Destination $runtimeDestination -Recurse -Force
    $runtimeVersions = [ordered]@{}
    foreach ($frameworkName in ($runtimeRequirements.Keys | Sort-Object)) {
        $versionDirectory = Get-ScannerCompatibleRuntimeDirectory -DotNetRoot $dotNetInstallRoot -FrameworkName $frameworkName -RequiredVersion $runtimeRequirements[$frameworkName]
        $frameworkDestination = Join-Path $runtimeDestination (Join-Path 'shared' $frameworkName)
        New-Item -ItemType Directory -Path $frameworkDestination -Force | Out-Null
        Copy-Item -LiteralPath $versionDirectory.FullName -Destination $frameworkDestination -Recurse -Force
        $runtimeVersions[$frameworkName] = $versionDirectory.Name
    }
    foreach ($notice in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
        $noticePath = Join-Path $dotNetInstallRoot $notice
        if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf)) {
            throw "Required .NET redistribution notice is missing: $noticePath"
        }
        Copy-Item -LiteralPath $noticePath -Destination $runtimeDestination
    }

    $contentRoot = Join-Path $contentSourceRoot 'tools\ScannerRoom\package-content'
    Copy-Item -LiteralPath (Join-Path $contentRoot 'README-FIRST.md') -Destination $packageStaging
    Copy-Item -LiteralPath (Join-Path $contentRoot 'instructions') -Destination $packageStaging -Recurse
    New-Item -ItemType Directory -Path (Join-Path $packageStaging 'scripts') -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $contentSourceRoot 'tools\ScannerRoom') -File -Filter '*.ps1' | Where-Object { $_.Name -ne 'Build-TestPackage.ps1' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $packageStaging 'scripts')
    }
    New-Item -ItemType Directory -Path (Join-Path $packageStaging 'notices') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $contentSourceRoot 'LICENSE.txt') -Destination (Join-Path $packageStaging 'notices\PROJECT-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $dotNetInstallRoot 'LICENSE.txt') -Destination (Join-Path $packageStaging 'notices\DOTNET-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $dotNetInstallRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $packageStaging 'notices\DOTNET-ThirdPartyNotices.txt')
    Copy-Item -LiteralPath (Join-Path $contentSourceRoot 'SCANNER_ROOM_ACCEPTANCE.md') -Destination (Join-Path $packageStaging 'instructions\SOURCE-ACCEPTANCE-LEDGER.md')
    Copy-Item -LiteralPath $testStatus -Destination (Join-Path $packageStaging 'TEST_STATUS.md')

    if (-not [string]::IsNullOrWhiteSpace($FixturePath)) {
        $fixtureDestination = Join-Path $packageStaging 'fixtures\scanner-room-v1'
        New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureDestination) -Force | Out-Null
        Copy-Item -LiteralPath ([System.IO.Path]::GetFullPath($FixturePath)) -Destination $fixtureDestination -Recurse -Force
        foreach ($backup in Get-ChildItem -LiteralPath $fixtureDestination -Recurse -Directory -Filter 'backups') {
            $backupPath = Assert-ScannerChildPath -Path $backup.FullName -Parent $fixtureDestination
            Remove-Item -LiteralPath $backupPath -Recurse -Force
        }
        foreach ($serverConfig in Get-ChildItem -LiteralPath $fixtureDestination -Recurse -File -Filter 'server.cfg') {
            $configText = Get-Content -LiteralPath $serverConfig.FullName -Raw
            $configText = $configText -replace '(?im)^(\s*(?:ServerPassword|AdminPassword)\s*[=:]\s*).+$', '$1'
            Set-Content -LiteralPath $serverConfig.FullName -Value $configText -Encoding UTF8
        }
    }

    New-Item -ItemType Directory -Path (Join-Path $packageStaging 'evidence-inbox') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $contentRoot 'EVIDENCE-INBOX.md') -Destination (Join-Path $packageStaging 'evidence-inbox\README.md')

    $buildInfo = [ordered]@{
        Schema = 'nitrox-ai-test-package-v2'
        PackageId = $packageId
        PackageMode = $Mode
        SourceCommit = $commit
        SourceShortCommit = $shortCommit
        SourceWorkspaceDirtyPathsAtPackaging = $dirtyPaths
        SourceWorkspaceExplicitlyAllowedDirtyPathsAtPackaging = $explicitlyAllowedDirtyPaths
        SourceWorkspaceFingerprint = $sourceFingerprint
        QualificationSha256 = $testStatusHash
        FixtureSha256 = $fixtureHash
        QualificationIdentity = $qualificationHash
        CreatedUtc = $createdUtc.ToString('o')
        Configuration = 'Release'
        RuntimeIdentifier = 'win-x64'
        GameTarget = 'Subnautica'
        GameBuildQualifiedLocally = $gameBuild
        DotNetSdk = (& $DotNetHost --version).Trim()
        BundledRuntimeVersions = $runtimeVersions
        EmbeddedAssemblyProductVersions = $embeddedVersions
        NetworkImpairmentProxySelfTestRequired = $true
        ScannerDiagnosticsSchema = 'SRD1 with per-process ep=<8hex> and monotonic n=<sequence>'
        BasePowerDiagnosticsSchema = 'BPD1 with per-process ep=<8hex>, monotonic n=<sequence>, and independent bounded source/audio budgets'
        EvidenceSummarySchema = 'scanner-summary-v3'
        TargetedRetest = 'instructions/GOTTEM-EMPTY-BASE-RECOVERY.md; formal Scanner Room and base-power matrices remain NOT_RUN'
        LauncherDataPathArgument = 'Supported by the launcher command parser; the start script passes a quoted package-specific data path.'
    }
    $buildInfo | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $packageStaging 'BUILD_INFO.json') -Encoding UTF8

    $manifestPath = Join-Path $packageStaging 'SHA256SUMS.txt'
    Get-ChildItem -LiteralPath $packageStaging -Recurse -File | Where-Object { $_.FullName -ne $manifestPath } | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageStaging.Length + 1).Replace('\', '/')
        '{0} *{1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    } | Set-Content -LiteralPath $manifestPath -Encoding ASCII

    & (Join-Path $packageStaging 'scripts\Verify-Package.ps1') -PackageRoot $packageStaging -Quiet

    New-Item -ItemType Directory -Path $destinationBase -Force | Out-Null
    if (Test-Path -LiteralPath $packageRoot) {
        if ((Get-Item -LiteralPath $packageRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to replace a reparse-point package: $packageRoot"
        }
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }
    foreach ($existingSidecar in @($zipPath, "$zipPath.sha256", "$zipPath.verify.txt")) {
        if (Test-Path -LiteralPath $existingSidecar) {
            Remove-Item -LiteralPath $existingSidecar -Force
        }
    }
    $destinationCopyStarted = $true
    Copy-Item -LiteralPath $packageStaging -Destination $packageRoot -Recurse -Force
    & (Join-Path $PSScriptRoot 'Verify-Package.ps1') -PackageRoot $packageRoot -TrustedManifestPath (Join-Path $packageStaging 'SHA256SUMS.txt') -Quiet

    Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    "$zipHash *$([System.IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ASCII
    @(
        'Verify this archive before extraction:',
        "  `$expected = '$zipHash'",
        "  `$actual = (Get-FileHash -LiteralPath '.\$([System.IO.Path]::GetFileName($zipPath))' -Algorithm SHA256).Hash",
        "  if (`$actual -ne `$expected) { throw 'Archive SHA-256 mismatch' }",
        '',
        'After extraction run .\scripts\Verify-Package.ps1.'
    ) | Set-Content -LiteralPath "$zipPath.verify.txt" -Encoding UTF8

    Write-Host "Package: $packageRoot"
    Write-Host "Archive: $zipPath"
    Write-Host "Archive SHA-256: $zipHash"
    [pscustomobject]@{ PackageRoot = $packageRoot; Archive = $zipPath; Sha256 = $zipHash; PackageId = $packageId }
}
catch {
    if ($destinationCopyStarted -and (Test-Path -LiteralPath $packageRoot)) {
        $safePartial = Assert-ScannerChildPath -Path $packageRoot -Parent $destinationBase
        Remove-Item -LiteralPath $safePartial -Recurse -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $staging) {
        $safeStaging = Assert-ScannerChildPath -Path $staging -Parent $stagingBase
        Remove-Item -LiteralPath $safeStaging -Recurse -Force
    }
}
