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

    if ($FilePath -ne "git") {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            $argText = $Arguments -join " "
            throw "Command failed (exit=$LASTEXITCODE): $FilePath $argText"
        }
        return
    }

    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $quotedArgs = $Arguments | ForEach-Object {
            if ($_ -match '\s') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
        }
        $argLine = $quotedArgs -join " "

        $stdoutFile = [System.IO.Path]::GetTempFileName()
        $stderrFile = [System.IO.Path]::GetTempFileName()
        try {
            $proc = Start-Process -FilePath $FilePath -ArgumentList $argLine -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
            $exitCode = $proc.ExitCode

            $stdoutText = ""
            $stderrText = ""
            if (Test-Path -LiteralPath $stdoutFile) {
                $stdoutText = [string](Get-Content -LiteralPath $stdoutFile -Raw)
                if ($null -eq $stdoutText) { $stdoutText = "" } else { $stdoutText = $stdoutText.Trim() }
            }
            if (Test-Path -LiteralPath $stderrFile) {
                $stderrText = [string](Get-Content -LiteralPath $stderrFile -Raw)
                if ($null -eq $stderrText) { $stderrText = "" } else { $stderrText = $stderrText.Trim() }
            }
            $outputText = @($stdoutText, $stderrText) -join [Environment]::NewLine
            $outputText = $outputText.Trim()
        }
        finally {
            if (Test-Path -LiteralPath $stdoutFile) { Remove-Item -LiteralPath $stdoutFile -Force }
            if (Test-Path -LiteralPath $stderrFile) { Remove-Item -LiteralPath $stderrFile -Force }
        }

        if (-not [string]::IsNullOrWhiteSpace($outputText)) {
            Write-Host $outputText
        }

        if ($exitCode -eq 0) {
            return
        }

        $isGitLockError = $FilePath -eq "git" -and (
            $outputText -match "index\.lock" -or
            $outputText -match "Permission denied"
        )

        if ($isGitLockError -and $attempt -lt $maxRetries) {
            Start-Sleep -Seconds 2
            continue
        }

        if ($isGitLockError) {
            throw "Git index lock/permission error while running: git $($Arguments -join ' ').`nClose GitHub Desktop or other git processes, then retry. If needed, verify no stale lock file exists at .git/index.lock."
        }

        $argText = $Arguments -join " "
        throw "Command failed (exit=$exitCode): $FilePath $argText"
    }
}

function Normalize-Version {
    param([string]$InputVersion)

    if ([string]::IsNullOrWhiteSpace($InputVersion)) {
        return $null
    }

    $v = $InputVersion.Trim()
    if ($v.StartsWith("v")) {
        $v = $v.Substring(1)
    }

    if (-not ($v -match '^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$')) {
        throw "Invalid version format: $InputVersion. Use SemVer like 1.0.2"
    }

    return $v
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

Invoke-Checked -FilePath "git" -Arguments @("rev-parse", "--is-inside-work-tree")

$normalizedVersion = Normalize-Version -InputVersion $Version

$repackageScript = Join-Path $PSScriptRoot "repackage-ugs.ps1"
if (-not (Test-Path -LiteralPath $repackageScript)) {
    throw "repackage script not found: $repackageScript"
}

$repackageArgs = @("-ExecutionPolicy", "Bypass", "-File", $repackageScript, "-Source", $Source, "-PackageRoot", $PackageRoot)
if ($normalizedVersion) {
    $repackageArgs += @("-Version", $normalizedVersion)
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

if ($normalizedVersion -and ($packageVersion -ne $normalizedVersion)) {
    throw "package.json version mismatch. expected=$normalizedVersion actual=$packageVersion"
}

$branchName = $Branch
if ([string]::IsNullOrWhiteSpace($branchName)) {
    $branchName = (& git rev-parse --abbrev-ref HEAD).Trim()
}

$tagName = "v$packageVersion"

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
