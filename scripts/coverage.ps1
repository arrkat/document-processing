# Windows/PowerShell twin of coverage.sh: runs every test project with coverlet and merges the results into one HTML report.
# Usage: scripts\coverage.ps1   ->   open TestResults\coverage-report\index.html
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')
$results = 'TestResults/coverage'
$report = 'TestResults/coverage-report'

Remove-Item -Recurse -Force $results, $report -ErrorAction SilentlyContinue
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory $results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet reportgenerator "-reports:$results/**/coverage.cobertura.xml" "-targetdir:$report" "-reporttypes:Html;TextSummary"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-Content "$report/Summary.txt"
Write-Host ""
Write-Host "HTML report: $report/index.html"
