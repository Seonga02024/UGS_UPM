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
$packageJsonPath = Join-Path $packagePath "package.json"

Assert-PathExists -Path $sourcePath -Label "UGS source folder"
Assert-PathExists -Path $packagePath -Label "Package root folder"
Assert-PathExists -Path $packageJsonPath -Label "package.json"

if (Test-Path -LiteralPath $runtimePath) {
    Get-ChildItem -LiteralPath $runtimePath -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $runtimePath | Out-Null
}

Copy-Item -Path (Join-Path $sourcePath "*") -Destination $runtimePath -Recurse -Force

if ($Version) {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $packageJson.version = $Version
    ($packageJson | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $packageJsonPath -Encoding UTF8
    Write-Host "Updated package version to $Version"
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
