#!/usr/bin/env bash
# Runs every test project with coverlet and merges the results into one HTML report.
# Usage: scripts/coverage.sh   →   open TestResults/coverage-report/index.html
set -euo pipefail

cd "$(dirname "$0")/.."
results=TestResults/coverage
report=TestResults/coverage-report

rm -rf "$results" "$report"
dotnet tool restore
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory "$results"

dotnet reportgenerator \
  -reports:"$results/**/coverage.cobertura.xml" \
  -targetdir:"$report" \
  -reporttypes:"Html;TextSummary"

cat "$report/Summary.txt"
echo
echo "HTML report: $report/index.html"
