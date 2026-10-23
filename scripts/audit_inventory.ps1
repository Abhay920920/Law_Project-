$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== 1. FILE EXTENSION SUMMARY ==="
$files = Get-ChildItem -Recurse -File -Path . | Where-Object { 
    $_.FullName -notmatch '\\(\.git|bin|obj|publish|Publish_Output)\\' 
}

$summary = $files | Group-Object Extension | Select-Object Name, Count, @{N='TotalBytes';E={($_.Group | Measure-Object Length -Sum).Sum}} | Sort-Object Count -Descending
$summary | Format-Table -AutoSize

Write-Host "`n=== 2. C# LINE COUNT BY SUBDIRECTORY ==="
$csFiles = $files | Where-Object { $_.Extension -eq '.cs' }
$csByDir = $csFiles | Group-Object { $_.Directory.Parent.Name + "\" + $_.Directory.Name } | Select-Object Name, Count, @{N='Lines';E={($_.Group | Get-Content | Measure-Object -Line).Lines}} | Sort-Object Lines -Descending
$csByDir | Format-Table -AutoSize

Write-Host "`n=== 3. RAZOR VIEW COUNT BY VIEW FOLDER ==="
$views = $files | Where-Object { $_.Extension -eq '.cshtml' }
$viewsByDir = $views | Group-Object { $_.Directory.Name } | Select-Object Name, Count, @{N='Lines';E={($_.Group | Get-Content | Measure-Object -Line).Lines}} | Sort-Object Lines -Descending
$viewsByDir | Format-Table -AutoSize

Write-Host "`n=== 4. TOTAL METRICS ==="
$totalLines = ($csFiles | Get-Content | Measure-Object -Line).Lines
$totalViewLines = ($views | Get-Content | Measure-Object -Line).Lines
Write-Host "Total C# Files: $($csFiles.Count) with $totalLines lines"
Write-Host "Total Razor Views: $($views.Count) with $totalViewLines lines"
Write-Host "Total Unique Source Files Audited: $($files.Count)"
