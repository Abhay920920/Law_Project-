$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. SQL INJECTION SCAN ==="
# Search for raw string concatenation or interpolation in SqlCommand or Dapper
$dalFiles = Get-ChildItem -Recurse -Filter *.cs -Path "Law Project\CaseManagement"
$sqlConcats = @()

foreach ($file in $dalFiles) {
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match 'SqlCommand\s*\(\s*\$"' -or 
            $line -match 'SqlCommand\s*\([^,]+?\+.*?\)' -or 
            $line -match 'QueryAsync<.*?>\(\s*\$"' -or 
            $line -match 'ExecuteAsync\(\s*\$"') {
            # Check if it contains dynamic interpolated variables
            if ($line -match '\{.*?\}' -and $line -notmatch '@\w+') {
                $sqlConcats += [PSCustomObject]@{
                    File = $file.Name
                    LineNumber = $i + 1
                    Code = $line.Trim()
                }
            }
        }
    }
}

if ($sqlConcats.Count -eq 0) {
    Write-Host "No raw string-interpolated dynamic SQL commands detected! All queries appear parameterized." -ForegroundColor Green
} else {
    Write-Host "Potential Raw SQL detected ($($sqlConcats.Count)):" -ForegroundColor Red
    $sqlConcats | Format-Table -AutoSize
}

Write-Host "`n=== 2. CONTROLLER AUTHORIZATION & CSRF AUDIT ==="
$controllers = Get-ChildItem -Filter *Controller.cs -Path "Law Project\CaseManagement\Controllers"
$ctrlSummary = foreach ($c in $controllers) {
    $content = Get-Content $c.FullName -Raw
    $hasAuthorizeClass = $content -match '\[Authorize'
    
    # Extract POST actions
    $postMatches = [regex]::Matches($content, '\[HttpPost\][\s\S]*?public\s+(?:async\s+)?(?:Task<)?(?:IActionResult|ActionResult|JsonResult)(?:>)?\s+(\w+)')
    $totalPosts = $postMatches.Count
    $csrfProtected = ([regex]::Matches($content, '\[ValidateAntiForgeryToken\]')).Count
    
    [PSCustomObject]@{
        Controller = $c.Name
        HasClassAuthorize = $hasAuthorizeClass
        HttpPostActions = $totalPosts
        ValidateAntiForgeryTokenCount = $csrfProtected
    }
}
$ctrlSummary | Format-Table -AutoSize

Write-Host "`n=== 3. HARDCODED SECRETS / PLAIN CREDENTIALS SCAN ==="
$secretPatterns = @(
    'password\s*=\s*"[^"]{4,}"',
    'secret\s*=\s*"[^"]{8,}"',
    'apikey\s*=\s*"[^"]{8,}"'
)

$suspiciousSecrets = @()
foreach ($file in $dalFiles) {
    $content = Get-Content $file.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        if ($line -match '(?i)(password|secret|apikey)\s*=\s*"([^"]+)"' -and 
            $line -notmatch 'Configuration\[' -and 
            $line -notmatch 'nameof\(' -and 
            $line -notmatch 'PasswordHash' -and 
            $line -notmatch 'string\.Empty' -and 
            $line -notmatch '""') {
            $suspiciousSecrets += [PSCustomObject]@{
                File = $file.Name
                Line = $i + 1
                Snippet = $line.Trim()
            }
        }
    }
}
Write-Host "Suspicious inline secrets count: $($suspiciousSecrets.Count)"
$suspiciousSecrets | Select-Object -First 15 | Format-Table -AutoSize
