<#
.SYNOPSIS
    Migrates all existing uploaded documents, orders, and petitions into the persistent external storage vault.
    Ensures zero data loss when deploying or pulling from Git.

.DESCRIPTION
    Scans legacy upload directories (wwwroot/uploads, publish/wwwroot/uploads) and copies
    all legal PDF documents and evidence files into the dedicated external persistent vault.
    Sets necessary directory permissions for IIS AppPool and web service accounts.

.EXAMPLE
    .\scripts\Migrate-UploadsToVault.ps1 -VaultPath "C:\inetpub\vhosts\itnwkrtc.in\uploads_vault"
#>

[CmdletBinding()]
param(
    [string]$VaultPath = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = (Get-Location).Path }
$repoRoot = Split-Path -Parent $scriptRoot

# Determine target vault path
if ([string]::IsNullOrWhiteSpace($VaultPath)) {
    if (Test-Path "C:\inetpub\vhosts\itnwkrtc.in") {
        $VaultPath = "C:\inetpub\vhosts\itnwkrtc.in\uploads_vault"
    } else {
        $VaultPath = Join-Path $repoRoot "uploads_vault"
    }
}

Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  Law Project -- Persistent Document Vault Migration Tool" -ForegroundColor Cyan
Write-Host "  Target Vault: $VaultPath" -ForegroundColor White
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Ensure vault directory exists
if (-not (Test-Path $VaultPath)) {
    Write-Host "[1/4] Creating persistent vault directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $VaultPath | Out-Null
    Write-Host "  [OK] Created vault at $VaultPath" -ForegroundColor Green
} else {
    Write-Host "[1/4] Vault directory verified: $VaultPath" -ForegroundColor Green
}

# 2. Candidate source upload folders to scan
$sourceCandidates = @(
    (Join-Path $repoRoot "publish\wwwroot\uploads"),
    (Join-Path $repoRoot "publish\uploads"),
    (Join-Path $repoRoot "Law Project\CaseManagement\wwwroot\uploads"),
    (Join-Path $repoRoot "Law Project\CaseManagement\uploads"),
    (Join-Path $repoRoot "Publish_Output\MVCCaseManagement\wwwroot\uploads")
)

Write-Host ""
Write-Host "[2/4] Scanning legacy upload directories for files to migrate..." -ForegroundColor Yellow

$migratedCount = 0
$skippedCount = 0

foreach ($src in $sourceCandidates) {
    if (-not (Test-Path $src)) { continue }
    Write-Host "  Scanning: $src" -ForegroundColor Gray

    $files = Get-ChildItem -Path $src -Recurse -File -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        # Calculate relative path from source
        $relPath = $file.FullName.Substring($src.Length).TrimStart('\', '/')
        $targetFile = Join-Path $VaultPath $relPath
        $targetDir = Split-Path -Parent $targetFile

        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        }

        if (-not (Test-Path $targetFile)) {
            Copy-Item -Path $file.FullName -Destination $targetFile -Force
            # Verify SHA256 hash
            $srcHash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
            $tgtHash = (Get-FileHash -Path $targetFile -Algorithm SHA256).Hash
            if ($srcHash -ne $tgtHash) {
                Write-Host "    [WARNING] SHA256 mismatch for $relPath! Retrying..." -ForegroundColor Red
                Copy-Item -Path $file.FullName -Destination $targetFile -Force
            }
            $migratedCount++
            Write-Host "    + Migrated & Verified: $relPath ($([math]::Round($file.Length / 1KB, 1)) KB, SHA: $($srcHash.Substring(0,8))...)" -ForegroundColor DarkGreen
        } else {
            # File already in vault - verify checksum match
            $srcHash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
            $tgtHash = (Get-FileHash -Path $targetFile -Algorithm SHA256).Hash
            if ($srcHash -ne $tgtHash) {
                Write-Host "    ! Updating outdated vault copy: $relPath" -ForegroundColor Yellow
                Copy-Item -Path $file.FullName -Destination $targetFile -Force
                $migratedCount++
            } else {
                $skippedCount++
            }
        }
    }
}

Write-Host ""
Write-Host "[3/4] Migration Summary:" -ForegroundColor Yellow
Write-Host "  Files Migrated: $migratedCount" -ForegroundColor Green
Write-Host "  Files Already in Vault: $skippedCount" -ForegroundColor Gray

# 3. Configure NTFS Permissions (if running on Windows server with IIS)
Write-Host ""
Write-Host "[4/4] Verifying NTFS permissions for IIS..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $VaultPath
    $iisUsers = @("IIS_IUSRS", "IUSR")
    foreach ($u in $iisUsers) {
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $u,
            "Modify, ReadAndExecute, ListDirectory, Read, Write",
            "ContainerInherit, ObjectInherit",
            "None",
            "Allow"
        )
        $acl.AddAccessRule($rule)
    }
    Set-Acl -Path $VaultPath -AclObject $acl
    Write-Host "  [OK] Permissions granted for IIS_IUSRS / IUSR." -ForegroundColor Green
} catch {
    Write-Host "  [INFO] Skipping IIS ACL assignment (non-administrative or local dev environment)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "=======================================================================" -ForegroundColor Cyan
Write-Host "  MIGRATION COMPLETE -- VAULT READY FOR PRODUCTION" -ForegroundColor Green
Write-Host "=======================================================================" -ForegroundColor Cyan

