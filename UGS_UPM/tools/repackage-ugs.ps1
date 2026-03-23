param(
    [string]$Source = "Assets/UGS",
    [string]$PackageRoot = "LocalPackages/com.robocare.ugs",
    [string]$Version,
    [switch]$Pack
)

$ErrorActionPreference = "Stop"

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourcePath = Join-Path $repoRoot $Source
$packagePath = Join-Path $repoRoot $PackageRoot
$runtimePath = Join-Path $packagePath "Runtime"
$runtimeMetaPath = Join-Path $packagePath "Runtime.meta"
$samplesRootPath = Join-Path $packagePath "Samples~"
$samplePath = Join-Path $samplesRootPath "UGS"
$packageJsonPath = Join-Path $packagePath "package.json"

Assert-PathExists -Path $sourcePath -Label "UGS source folder"
Assert-PathExists -Path $packagePath -Label "Package root folder"
Assert-PathExists -Path $packageJsonPath -Label "package.json"

# Runtime removed: package works via Samples import
if (Test-Path -LiteralPath $runtimePath) {
    Remove-Item -LiteralPath $runtimePath -Recurse -Force
}
if (Test-Path -LiteralPath $runtimeMetaPath) {
    Remove-Item -LiteralPath $runtimeMetaPath -Force
}

# Samples = full UGS folder (scripts + prefabs + resources)
if (-not (Test-Path -LiteralPath $samplesRootPath)) {
    New-Item -ItemType Directory -Path $samplesRootPath | Out-Null
}
if (Test-Path -LiteralPath $samplePath) {
    Remove-Item -LiteralPath $samplePath -Recurse -Force
}
New-Item -ItemType Directory -Path $samplePath | Out-Null

Copy-Item -Path (Join-Path $sourcePath "*") -Destination $samplePath -Recurse -Force

$sampleReadmePath = Join-Path $samplePath "README.md"
@"
# UGS Full Sample

Import this sample to copy full UGS content into your project sample area.

Included:
- `Common Script/*`
- `Common Prefabs/*`
- `Common Resources/*` (if present)
"@ | Set-Content -Path $sampleReadmePath -Encoding UTF8

if ($Version) {
    $normalized = $Version.Trim()
    if ($normalized.StartsWith("v")) {
        $normalized = $normalized.Substring(1)
    }

    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $packageJson.version = $normalized
    ($packageJson | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $packageJsonPath -Encoding UTF8
    Write-Host "Updated package version to $normalized"
}

if ($Pack) {
    Push-Location $packagePath
    try {
        $localNpmCache = Join-Path $repoRoot ".tmp/npm-cache-ugs"
        if (-not (Test-Path -LiteralPath $localNpmCache)) {
            New-Item -ItemType Directory -Path $localNpmCache | Out-Null
        }

        Get-ChildItem -LiteralPath $packagePath -Filter "*.tgz" -File | Remove-Item -Force
        npm pack --cache "$localNpmCache"
        if ($LASTEXITCODE -ne 0) {
            throw "npm pack failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host "UGS package sync complete."
Write-Host "Source : $sourcePath"
Write-Host "Package: $packagePath"
