[CmdletBinding()]
param(
    [switch]$SelfContained = $false,
    [switch]$SkipTests = $false,
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
Write-Host "[1/4] Compiling MVCCaseManagement and NapixEcourtsApi (Release)..." -ForegroundColor Yellow
dotnet build "$scriptDir\Law Project\CaseManagement\MVCCaseManagement.csproj" -c Release --no-restore --nologo -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    dotnet build "$scriptDir\Law Project\CaseManagement\MVCCaseManagement.csproj" -c Release --nologo -p:NuGetAudit=false
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed for MVCCaseManagement with exit code $LASTEXITCODE"
}
dotnet build "$scriptDir\NapixEcourtsApi\NapixEcourtsApi.csproj" -c Release --no-restore --nologo -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    dotnet build "$scriptDir\NapixEcourtsApi\NapixEcourtsApi.csproj" -c Release --nologo -p:NuGetAudit=false
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed for NapixEcourtsApi with exit code $LASTEXITCODE"
}
Write-Host "  [OK] Build completed successfully." -ForegroundColor Green

# Step 2: Automated Tests
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "[2/4] Running automated unit tests..." -ForegroundColor Yellow
    dotnet test "$PSScriptRoot\LawProject.sln" -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed! Publishing aborted to protect production integrity."
    }
    Write-Host "  [OK] All test suites passed." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/4] Skipping unit tests (-SkipTests specified)..." -ForegroundColor Yellow
}

# Clean existing outputs while safely preserving any existing local uploads
$tempBackupUploads = Join-Path ([System.IO.Path]::GetTempPath()) "LawProject_Uploads_Backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
$existingUploadsDir = Join-Path $webOut "wwwroot\uploads"
if (Test-Path $existingUploadsDir) {
    Copy-Item -Path $existingUploadsDir -Destination $tempBackupUploads -Recurse -Force
}

if (Test-Path $webOut) { Remove-Item -Recurse -Force $webOut }
if (Test-Path $apiOut) { Remove-Item -Recurse -Force $apiOut }

# Step 3: Publish Web Portal
Write-Host ""
Write-Host "[3/4] Publishing MVCCaseManagement (Web MVC Portal)..." -ForegroundColor Yellow
$webArgs = @(
    "publish",
    "$scriptDir\Law Project\CaseManagement\MVCCaseManagement.csproj",
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

# Restore preserved uploads if any were backed up
if (Test-Path $tempBackupUploads) {
    Copy-Item -Path "$tempBackupUploads\*" -Destination (Join-Path $webOut "wwwroot\uploads") -Recurse -Force
    Remove-Item -Recurse -Force $tempBackupUploads -ErrorAction SilentlyContinue
}

# Copy App_Data (including persistent NAPIX party search master store and cause list caches)
$sourceAppData = Join-Path $scriptDir "Law Project\CaseManagement\App_Data"
$targetAppData = Join-Path $webOut "App_Data"
if (Test-Path $sourceAppData) {
    Write-Host "  Syncing App_Data (NAPIX persistent caches) to $targetAppData..." -ForegroundColor Gray
    if (-not (Test-Path $targetAppData)) { New-Item -ItemType Directory -Force -Path $targetAppData | Out-Null }
    Copy-Item -Path "$sourceAppData\*" -Destination $targetAppData -Recurse -Force
}

# Copy fresh Views to webOut and rootPublish for Razor Runtime Compilation
$sourceViews = Join-Path $scriptDir "Law Project\CaseManagement\Views"
$targetWebViews = Join-Path $webOut "Views"
Write-Host "  Syncing latest Razor Views to $targetWebViews..." -ForegroundColor Gray
if (-not (Test-Path $targetWebViews)) { New-Item -ItemType Directory -Force -Path $targetWebViews | Out-Null }
Copy-Item -Path "$sourceViews\*" -Destination $targetWebViews -Recurse -Force

$rootPublish = Join-Path $scriptDir "publish"
if (Test-Path $rootPublish) {
    Write-Host "  Syncing to $rootPublish (preserving existing uploads)..." -ForegroundColor Gray
    # Copy all binaries and views while preserving existing files in rootPublish uploads
    Get-ChildItem -Path $webOut | Where-Object { $_.Name -notin @("uploads") } | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $rootPublish -Recurse -Force
    }
    # Ensure uploads directory structure exists in rootPublish without overwriting existing files
    $rootUploads = Join-Path $rootPublish "wwwroot\uploads"
    if (-not (Test-Path $rootUploads)) { New-Item -ItemType Directory -Force -Path $rootUploads | Out-Null }
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
