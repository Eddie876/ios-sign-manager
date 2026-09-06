param(
    [string]$BackupPath = "./backups/sign-manager-backup.zip",
    [string]$RestoreRoot = "./data-restore"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $BackupPath)) {
    throw "Backup archive not found: $BackupPath"
}

New-Item -ItemType Directory -Path $RestoreRoot -Force | Out-Null
Expand-Archive -Path $BackupPath -DestinationPath $RestoreRoot -Force
Write-Host "Backup restored to $RestoreRoot"
