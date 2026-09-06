param(
    [string]$DataRoot = "./data",
    [string]$OutputPath = "./backups/sign-manager-backup.zip"
)

$ErrorActionPreference = "Stop"

$fullDataRoot = (Resolve-Path -Path $DataRoot).Path
$outputDir = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if (Test-Path -Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

Compress-Archive -Path (Join-Path $fullDataRoot "*") -DestinationPath $OutputPath -CompressionLevel Optimal
Write-Host "Backup created at $OutputPath"
