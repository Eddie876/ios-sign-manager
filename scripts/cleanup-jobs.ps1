param(
    [string]$WorkspaceRoot = "./data/jobs",
    [int]$MaxAgeHours = 72
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $WorkspaceRoot)) {
    Write-Host "Workspace root not found: $WorkspaceRoot"
    exit 0
}

$threshold = (Get-Date).ToUniversalTime().AddHours(-$MaxAgeHours)
$deleted = 0

Get-ChildItem -Path $WorkspaceRoot -Directory | ForEach-Object {
    if ($_.LastWriteTimeUtc -lt $threshold) {
        Remove-Item -Path $_.FullName -Recurse -Force
        $deleted++
    }
}

Write-Host "Cleanup completed. Deleted $deleted directories."
