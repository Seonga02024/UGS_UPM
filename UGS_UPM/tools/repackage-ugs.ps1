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
$samplesRootPath = Join-Path $packagePath "Samples~"
$samplePrefabsPath = Join-Path $samplesRootPath "UGS"
$packageJsonPath = Join-Path $packagePath "package.json"

$sourceScriptPath = Join-Path $sourcePath "Common Script"
$sourcePrefabsPath = Join-Path $sourcePath "Common Prefabs"
$sourceResourcesPath = Join-Path $sourcePath "Common Resources"

Assert-PathExists -Path $sourcePath -Label "UGS source folder"
Assert-PathExists -Path $sourceScriptPath -Label "UGS Common Script folder"
Assert-PathExists -Path $sourcePrefabsPath -Label "UGS Common Prefabs folder"
Assert-PathExists -Path $packagePath -Label "Package root folder"
Assert-PathExists -Path $packageJsonPath -Label "package.json"

# Runtime = scripts only
if (Test-Path -LiteralPath $runtimePath) {
    Get-ChildItem -LiteralPath $runtimePath -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $runtimePath | Out-Null
}

Copy-Item -Path $sourceScriptPath -Destination $runtimePath -Recurse -Force

# Samples = prefabs/examples only (no scripts)
if (-not (Test-Path -LiteralPath $samplesRootPath)) {
    New-Item -ItemType Directory -Path $samplesRootPath | Out-Null
}
if (Test-Path -LiteralPath $samplePrefabsPath) {
    Remove-Item -LiteralPath $samplePrefabsPath -Recurse -Force
}
New-Item -ItemType Directory -Path $samplePrefabsPath | Out-Null

Copy-Item -Path $sourcePrefabsPath -Destination $samplePrefabsPath -Recurse -Force
if (Test-Path -LiteralPath $sourceResourcesPath) {
    Copy-Item -Path $sourceResourcesPath -Destination $samplePrefabsPath -Recurse -Force
}

$sampleReadmePath = Join-Path $samplePrefabsPath "README.md"
@"
# UGS Sample

Import this sample to copy UGS prefab assets into your project sample area.

Included:
- `Common Prefabs/*`
- `Common Resources/*` (if present)

Excluded:
- Scripts (Runtime provides scripts to avoid duplicate compilation)
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
        Get-ChildItem -LiteralPath $packagePath -Filter "*.tgz" -File | Remove-Item -Force
        npm pack
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

