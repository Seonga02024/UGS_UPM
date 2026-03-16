param(
    [string]$Source = "Assets/UGS",
    [string]$PackageRoot = "LocalPackages/com.robocare.ugs",
    [string]$Version,
    [string]$Remote = "origin",
    [string]$Branch,
    [switch]$NoPack,
    [switch]$NoCommit,
    [switch]$NoTag,
    [switch]$NoPush,
    [switch]$CreateRelease
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        $argText = $Arguments -join " "
        throw "Command failed: $FilePath $argText"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

Invoke-Checked -FilePath "git" -Arguments @("rev-parse", "--is-inside-work-tree")

$repackageScript = Join-Path $PSScriptRoot "repackage-ugs.ps1"
if (-not (Test-Path -LiteralPath $repackageScript)) {
    throw "repackage script not found: $repackageScript"
}

$repackageArgs = @("-ExecutionPolicy", "Bypass", "-File", $repackageScript, "-Source", $Source, "-PackageRoot", $PackageRoot)
if ($Version) {
    $repackageArgs += @("-Version", $Version)
}
if (-not $NoPack) {
    $repackageArgs += "-Pack"
}

Invoke-Checked -FilePath "powershell" -Arguments $repackageArgs

$packageJsonPath = Join-Path $repoRoot (Join-Path $PackageRoot "package.json")
if (-not (Test-Path -LiteralPath $packageJsonPath)) {
    throw "package.json not found: $packageJsonPath"
}

$packageInfo = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$packageName = $packageInfo.name
$packageVersion = $packageInfo.version
if ([string]::IsNullOrWhiteSpace($packageName) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "package name/version missing in package.json"
}

$branchName = $Branch
if ([string]::IsNullOrWhiteSpace($branchName)) {
    $branchName = (& git rev-parse --abbrev-ref HEAD).Trim()
}

$tagName = "$packageName/v$packageVersion"

if (-not $NoCommit) {
    Invoke-Checked -FilePath "git" -Arguments @("add", $PackageRoot)
    if (Test-Path -LiteralPath "tools/repackage-ugs.ps1") {
        Invoke-Checked -FilePath "git" -Arguments @("add", "tools/repackage-ugs.ps1")
    }
    if (Test-Path -LiteralPath "tools/publish-ugs-upm.ps1") {
        Invoke-Checked -FilePath "git" -Arguments @("add", "tools/publish-ugs-upm.ps1")
    }
    if (Test-Path -LiteralPath "Assets/Editor/UgsPackageAutomationWindow.cs") {
        Invoke-Checked -FilePath "git" -Arguments @("add", "Assets/Editor/UgsPackageAutomationWindow.cs")
    }

    & git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "No staged changes. Commit skipped."
    }
    else {
        Invoke-Checked -FilePath "git" -Arguments @("commit", "-m", "chore: release $packageName $packageVersion")
    }
}

if (-not $NoTag) {
    $existingTag = (& git tag -l $tagName).Trim()
    if ($existingTag) {
        Write-Host "Tag already exists: $tagName"
    }
    else {
        Invoke-Checked -FilePath "git" -Arguments @("tag", "-a", $tagName, "-m", "Release $packageName $packageVersion")
    }
}

if (-not $NoPush) {
    Invoke-Checked -FilePath "git" -Arguments @("push", $Remote, $branchName)
    if (-not $NoTag) {
        Invoke-Checked -FilePath "git" -Arguments @("push", $Remote, $tagName)
    }
}

if ($CreateRelease) {
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $ghCommand) {
        throw "GitHub CLI (gh) not found. Install gh or run without -CreateRelease."
    }

    Invoke-Checked -FilePath "gh" -Arguments @("release", "create", $tagName, "--title", $tagName, "--notes", "Release $packageName $packageVersion")
}

Write-Host "Publish automation complete."
Write-Host "Package: $packageName"
Write-Host "Version: $packageVersion"
Write-Host "Tag    : $tagName"
