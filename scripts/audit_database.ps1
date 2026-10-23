$sql = Get-Content 'Law Project\CaseManagement\SQL\00_Master_Full_Database_Schema_And_Seed.sql' -Raw
$tableMatches = [regex]::Matches($sql, '(?i)CREATE\s+TABLE\s+(?:dbo\.)?\[?(\w+)\]?')
$tables = @()
foreach ($m in $tableMatches) { $tables += $m.Groups[1].Value }

$pks = [regex]::Matches($sql, '(?i)PRIMARY\s+KEY').Count
$fks = [regex]::Matches($sql, '(?i)FOREIGN\s+KEY').Count
$indexes = [regex]::Matches($sql, '(?i)CREATE\s+(?:UNIQUE\s+)?(?:NONCLUSTERED\s+)?INDEX').Count
$procedures = [regex]::Matches($sql, '(?i)CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+\[?(\w+)\]?').Count

Write-Host "Total Table Declarations: $($tables.Count)"
Write-Host "Primary Key Constraints: $pks"
Write-Host "Foreign Key Constraints: $fks"
Write-Host "Non-Clustered Indexes: $indexes"
Write-Host "Stored Procedures: $procedures"

Write-Host "`nSample Tables:"
$tables | Select-Object -Unique | Select-Object -First 25
