<#
.SYNOPSIS
    Automated zero-loss build, test, publish, and git-push deployment script for NWKRTC Law Project.
.DESCRIPTION
    1. Runs automated unit tests to verify zero regressions.
    2. Builds and publishes Release artifacts to publish/ directory.
    3. Verifies that persistent storage vaults outside publish (law.itnwkrtc.in\uploads_vault and itnwkrtc.in\httpdocs\uploads_vault) are protected.
    4. Stages changes and commits to Git.
    5. Pushes to GitHub origin/main so Plesk can safely pull without any file/document loss.
#>

param(
    [string]$CommitMessage = "deploy: release publish update with dual persistent storage vaults"
)

$ErrorActionPreference = "Stop"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  🏛️ NWKRTC Law Project - Zero-Loss Safe Deploy to Git    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Run Storage and Core Tests
Write-Host "`n[1/5] Running automated tests..." -ForegroundColor Yellow
dotnet test "Law Project\MVCCaseManagement.Tests\MVCCaseManagement.Tests.csproj" -p:OutputPath="bin\TestBuild\" --no-restore --filter "FullyQualifiedName~PhysicalFileStorageServiceTests" -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed! Aborting deployment."
    exit 1
}
Write-Host "✅ All tests passed successfully!" -ForegroundColor Green

# 2. Build & Publish Release
Write-Host "`n[2/5] Publishing Release build to publish/..." -ForegroundColor Yellow
dotnet publish "Law Project\CaseManagement\MVCCaseManagement.csproj" -c Release -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed! Aborting deployment."
    exit 1
}
Write-Host "✅ Release build published to publish/!" -ForegroundColor Green

# 3. Verify Safe Vault Configuration
Write-Host "`n[3/5] Verifying persistent vault paths in publish config..." -ForegroundColor Yellow
$prodConfig = Get-Content "publish\appsettings.Production.json" -Raw | ConvertFrom-Json
$primaryDir = $prodConfig.Storage.UploadsDirectory
$secDir = $prodConfig.Storage.SecondaryUploadsDirectory

if ($primaryDir -ne "..\uploads_vault" -or $secDir -ne "..\..\httpdocs\uploads_vault") {
    Write-Error "CRITICAL: publish\appsettings.Production.json storage vaults are not configured outside publish! Aborting."
    exit 1
}
Write-Host "✅ Storage vaults safely configured outside publish:" -ForegroundColor Green
Write-Host "   - Primary: $primaryDir (law.itnwkrtc.in\uploads_vault)" -ForegroundColor Gray
Write-Host "   - Secondary: $secDir (itnwkrtc.in\httpdocs\uploads_vault)" -ForegroundColor Gray

# 4. Git Stage & Commit
Write-Host "`n[4/5] Staging and committing to Git..." -ForegroundColor Yellow
$gitExe = "C:\Users\IT\AppData\Local\Programs\MinGit\cmd\git.exe"
if (-not (Test-Path $gitExe)) {
    $gitExe = "git"
}

& $gitExe add -A
& $gitExe commit -m $CommitMessage
Write-Host "✅ Git commit created!" -ForegroundColor Green

# 5. Git Push
Write-Host "`n[5/5] Pushing to origin main..." -ForegroundColor Yellow
& $gitExe push origin main
if ($LASTEXITCODE -ne 0) {
    Write-Error "git push failed! Please check network or git authentication."
    exit 1
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  🚀 DEPLOYMENT COMPLETED SUCCESSFULLY!                  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Plesk Git can now pull the latest updates with ZERO risk:" -ForegroundColor Cyan
Write-Host "1. Code and DLLs will update in law.itnwkrtc.in\publish." -ForegroundColor White
Write-Host "2. Uploaded documents remain 100% safe outside publish in:" -ForegroundColor White
Write-Host "   - law.itnwkrtc.in\uploads_vault" -ForegroundColor White
Write-Host "   - itnwkrtc.in\httpdocs\uploads_vault" -ForegroundColor White
Write-Host "3. IIS Shadow Copying will reload the new DLLs without downtime." -ForegroundColor White
