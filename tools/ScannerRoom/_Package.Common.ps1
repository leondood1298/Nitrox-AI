Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ScannerPackageRoot {
    param([string]$ScriptDirectory = $PSScriptRoot)

    return [System.IO.Path]::GetFullPath((Join-Path $ScriptDirectory '..'))
}

function Get-ScannerBuildInfo {
    param([Parameter(Mandatory = $true)][string]$PackageRoot)

    $buildInfoPath = Join-Path $PackageRoot 'BUILD_INFO.json'
    if (-not (Test-Path -LiteralPath $buildInfoPath -PathType Leaf)) {
        throw "Package metadata is missing: $buildInfoPath"
    }
    return Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
}

function ConvertTo-SafeScannerLabel {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [string]$Fallback = 'unknown'
    )

    $label = $Value.Trim().ToLowerInvariant() -replace '[^a-z0-9_-]', '-'
    $label = $label.Trim('-')
    if ([string]::IsNullOrWhiteSpace($label)) {
        return $Fallback
    }
    return $label
}

function Assert-ScannerChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $requiredPrefix = $resolvedParent + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside expected parent '$resolvedParent': $resolvedPath"
    }
    return $resolvedPath
}

function Set-ScannerBundledRuntime {
    param([Parameter(Mandatory = $true)][string]$PackageRoot)

    $runtimeRoot = Join-Path $PackageRoot 'runtime'
    $dotnetHostPath = Join-Path $runtimeRoot 'dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetHostPath -PathType Leaf)) {
        throw "Bundled .NET runtime is missing: $dotnetHostPath"
    }
    $env:DOTNET_ROOT = $runtimeRoot
    $env:DOTNET_ROOT_X64 = $runtimeRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
}

function Get-ScannerRunRoot {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [string]$Override
    )

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        return [System.IO.Path]::GetFullPath($Override)
    }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw 'LOCALAPPDATA is unavailable; pass -RunRoot explicitly.'
    }
    return Join-Path $env:LOCALAPPDATA (Join-Path 'Nitrox-AI-TestRuns' $PackageId)
}

function Get-ScannerEvidenceInbox {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [string]$Override
    )

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        return [System.IO.Path]::GetFullPath($Override)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:NITROX_SCANNER_EVIDENCE_INBOX)) {
        return [System.IO.Path]::GetFullPath($env:NITROX_SCANNER_EVIDENCE_INBOX)
    }
    throw 'No synced evidence inbox is configured. Pass -OutputInbox or set NITROX_SCANNER_EVIDENCE_INBOX to the Google Drive package evidence-inbox path.'
}

function Get-ScannerRuntimeRequirements {
    param([Parameter(Mandatory = $true)][string[]]$RuntimeConfigPaths)

    $requirements = @{}
    foreach ($runtimeConfigPath in $RuntimeConfigPaths) {
        if (-not (Test-Path -LiteralPath $runtimeConfigPath -PathType Leaf)) {
            throw "Runtime configuration is missing: $runtimeConfigPath"
        }
        $config = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
        $frameworks = @()
        if ($config.runtimeOptions.PSObject.Properties.Name -contains 'framework' -and $null -ne $config.runtimeOptions.framework) {
            $frameworks += $config.runtimeOptions.framework
        }
        if ($config.runtimeOptions.PSObject.Properties.Name -contains 'frameworks' -and $null -ne $config.runtimeOptions.frameworks) {
            $frameworks += @($config.runtimeOptions.frameworks)
        }
        foreach ($framework in $frameworks) {
            if ([string]::IsNullOrWhiteSpace($framework.name) -or [string]::IsNullOrWhiteSpace($framework.version)) {
                throw "Malformed framework requirement in $runtimeConfigPath"
            }
            [Version]$requiredVersion = $null
            if (-not [Version]::TryParse([string]$framework.version, [ref]$requiredVersion)) {
                throw "Invalid framework version '$($framework.version)' in $runtimeConfigPath"
            }
            if (-not $requirements.ContainsKey([string]$framework.name) -or $requiredVersion -gt $requirements[[string]$framework.name]) {
                $requirements[[string]$framework.name] = $requiredVersion
            }
        }
    }
    return $requirements
}

function Get-ScannerCompatibleRuntimeDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$DotNetRoot,
        [Parameter(Mandatory = $true)][string]$FrameworkName,
        [Parameter(Mandatory = $true)][Version]$RequiredVersion
    )

    $frameworkRoot = Join-Path $DotNetRoot (Join-Path 'shared' $FrameworkName)
    if (-not (Test-Path -LiteralPath $frameworkRoot -PathType Container)) {
        throw "Required runtime framework is missing: $FrameworkName"
    }
    $compatible = foreach ($directory in Get-ChildItem -LiteralPath $frameworkRoot -Directory) {
        [Version]$candidateVersion = $null
        if ([Version]::TryParse($directory.Name, [ref]$candidateVersion) -and
            $candidateVersion.Major -eq $RequiredVersion.Major -and
            $candidateVersion.Minor -eq $RequiredVersion.Minor -and
            $candidateVersion -ge $RequiredVersion) {
            [pscustomobject]@{ Directory = $directory; Version = $candidateVersion }
        }
    }
    $selected = $compatible | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $selected) {
        throw "No compatible $FrameworkName runtime satisfies $RequiredVersion under $frameworkRoot"
    }
    return $selected.Directory
}

function Assert-ScannerRuntimeBundle {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [Parameter(Mandatory = $true)][string[]]$RuntimeConfigPaths
    )

    $runtimeRoot = Join-Path $PackageRoot 'runtime'
    $requirements = Get-ScannerRuntimeRequirements -RuntimeConfigPaths $RuntimeConfigPaths
    foreach ($frameworkName in $requirements.Keys) {
        [void](Get-ScannerCompatibleRuntimeDirectory -DotNetRoot $runtimeRoot -FrameworkName $frameworkName -RequiredVersion $requirements[$frameworkName])
    }
    return $requirements
}

function Get-ScannerGameBuild {
    param([Parameter(Mandatory = $true)][string]$GamePath)

    $root = [System.IO.Path]::GetFullPath($GamePath)
    $versionPath = Join-Path $root 'Subnautica_Data\StreamingAssets\SNUnmanagedData\plastic_status.ignore'
    $gameExe = Join-Path $root 'Subnautica.exe'
    if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf) -or -not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "Subnautica installation is incomplete or unsupported: $root"
    }
    $versionText = (Get-Content -LiteralPath $versionPath -TotalCount 1).Trim()
    $gameBuild = 0
    if (-not [int]::TryParse($versionText, [ref]$gameBuild)) {
        throw "Could not parse Subnautica build from $versionPath"
    }
    return $gameBuild
}

function Assert-ScannerExpectedGameBuild {
    param(
        [Parameter(Mandatory = $true)]$BuildInfo,
        [Parameter(Mandatory = $true)][string]$GamePath
    )

    $actual = Get-ScannerGameBuild -GamePath $GamePath
    if ($BuildInfo.PSObject.Properties.Name -contains 'GameBuildQualifiedLocally') {
        $expected = [int]$BuildInfo.GameBuildQualifiedLocally
        if ($expected -gt 0 -and $actual -ne $expected) {
            throw "Subnautica build mismatch. Package requires build $expected but '$GamePath' is build $actual."
        }
    }
    return $actual
}

function Protect-ScannerEvidenceText {
    param([AllowEmptyString()][string]$Text)

    if ($null -eq $Text) {
        return ''
    }
    # Preserve valid JSON for both quoted and non-string sensitive values.
    $protected = $Text -replace '(?i)("(?:serverpassword|adminpassword|password|access[_-]?token|refresh[_-]?token|secret)"\s*:\s*)"(?:\\.|[^"\\])*"', '$1"<redacted>"'
    $protected = $protected -replace '(?i)("(?:serverpassword|adminpassword|password|access[_-]?token|refresh[_-]?token|secret)"\s*:\s*)(?!")[^,\s}]+', '$1"<redacted>"'
    # Config and log fields can use either quote style or an unquoted token.
    $protected = $protected -replace '(?i)(\b(?:serverpassword|adminpassword|password|access[_-]?token|refresh[_-]?token|secret)\b\s*[=:]\s*)"(?:\\.|[^"\\])*"', '$1"<redacted>"'
    $protected = $protected -replace '(?i)(\b(?:serverpassword|adminpassword|password|access[_-]?token|refresh[_-]?token|secret)\b\s*[=:]\s*)''[^'']*''', '$1''<redacted>'''
    $protected = $protected -replace '(?i)(\b(?:serverpassword|adminpassword|password|access[_-]?token|refresh[_-]?token|secret)\b\s*[=:]\s*)[^"''\s,;|}]+', '$1<redacted>'
    # Match both normal Windows paths and JSON-escaped paths (one or more
    # backslashes) without emitting invalid escape sequences into JSON.
    $protected = $protected -replace '(?i)\bC:\\+Users\\+[^\\/"\s]+', 'C:<user>'
    return $protected
}
