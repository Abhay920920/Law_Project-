[CmdletBinding()]
param(
    [switch]$SelfContained = $false,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if (-not $scriptDir) {
    $scriptDir = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $scriptDir "Publish_Output"
}

$mode = "Framework-Dependent (.NET 9)"
if ($SelfContained) {
    $mode = "Self-Contained (win-x64)"
}

Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  Law Project -- Automated Production Publishing Pipeline" -ForegroundColor Cyan
Write-Host "  Mode: $mode" -ForegroundColor Cyan
Write-Host "  Target Output: $OutputDir" -ForegroundColor Cyan
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host ""

$webOut = Join-Path $OutputDir "MVCCaseManagement"
$apiOut = Join-Path $OutputDir "NapixEcourtsApi"

# Step 1: Solution Build
Write-Host "[1/4] Compiling LawProject.sln (Release)..." -ForegroundColor Yellow
dotnet build "$scriptDir\LawProject.sln" -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
Write-Host "  [OK] Build completed successfully." -ForegroundColor Green

# Step 2: Automated Tests
Write-Host ""
Write-Host "[2/4] Running automated unit tests..." -ForegroundColor Yellow
dotnet test "$PSScriptRoot\LawProject.sln" -c Release --no-build --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit tests failed! Publishing aborted to protect production integrity."
}
Write-Host "  [OK] All test suites passed." -ForegroundColor Green

# Clean existing outputs
if (Test-Path $webOut) { Remove-Item -Recurse -Force $webOut }
if (Test-Path $apiOut) { Remove-Item -Recurse -Force $apiOut }

# Step 3: Publish Web Portal
Write-Host ""
Write-Host "[3/4] Publishing MVCCaseManagement (Web MVC Portal)..." -ForegroundColor Yellow
$webArgs = @(
    "publish",
    "$scriptDir\Law Project\MVCCaseManagement\MVCCaseManagement.csproj",
    "-c", "Release",
    "-o", $webOut,
    "--nologo"
)
if ($SelfContained) {
    $webArgs += @("-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=false")
} else {
    $webArgs += @("--no-build")
}
& dotnet $webArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publishing MVCCaseManagement failed with exit code $LASTEXITCODE"
}
Write-Host "  [OK] MVCCaseManagement published." -ForegroundColor Green

# Step 4: Publish NapixEcourtsApi
Write-Host ""
Write-Host "[4/4] Publishing NapixEcourtsApi (Microservice)..." -ForegroundColor Yellow
$apiArgs = @(
    "publish",
    "$scriptDir\NapixEcourtsApi\NapixEcourtsApi.csproj",
    "-c", "Release",
    "-o", $apiOut,
    "--nologo"
)
if ($SelfContained) {
    $apiArgs += @("-r", "win-x64", "--self-contained", "true")
} else {
    $apiArgs += @("--no-build")
}
& dotnet $apiArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publishing NapixEcourtsApi failed with exit code $LASTEXITCODE"
}
Write-Host "  [OK] NapixEcourtsApi published." -ForegroundColor Green

# Ensure directories for storage and diagnostics
New-Item -ItemType Directory -Force -Path (Join-Path $webOut "logs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $webOut "uploads") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $webOut "wwwroot\uploads") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $apiOut "logs") | Out-Null

$rootPublish = Join-Path $scriptDir "publish"
if (Test-Path $rootPublish) {
    Write-Host "  Syncing to $rootPublish..." -ForegroundColor Gray
    Copy-Item -Path "$webOut\*" -Destination $rootPublish -Recurse -Force
}

Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  PUBLISH COMPLETE!" -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  Web Application:      $webOut" -ForegroundColor White
Write-Host "  eCourts Microservice: $apiOut" -ForegroundColor White
Write-Host ""
Write-Host "  To host on IIS or deploy, consult docs/PUBLISHING_GUIDE.md." -ForegroundColor DarkGray
Write-Host ""
