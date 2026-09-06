param(
    [string]$BackupPath = "./backups/sign-manager-backup.zip",
    [string]$RestoreRoot = "./data-restore",
    [string]$SigningStateRoot = "./signing-state-restore"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $BackupPath)) {
    throw "Backup archive not found: $BackupPath"
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sign-manager-restore-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

try {
    Expand-Archive -Path $BackupPath -DestinationPath $stagingRoot -Force

    New-Item -ItemType Directory -Path $RestoreRoot -Force | Out-Null
    $stagingData = Join-Path $stagingRoot "data"
    if (Test-Path -Path $stagingData) {
        Copy-Item -Path (Join-Path $stagingData "*") -Destination $RestoreRoot -Recurse -Force
    }
    else {
        # Backward compatibility with older archives that stored data at root.
        Copy-Item -Path (Join-Path $stagingRoot "*") -Destination $RestoreRoot -Recurse -Force
    }

    $stagingSigningState = Join-Path $stagingRoot "signing-state"
    if (Test-Path -Path $stagingSigningState) {
        New-Item -ItemType Directory -Path $SigningStateRoot -Force | Out-Null
        Copy-Item -Path (Join-Path $stagingSigningState "*") -Destination $SigningStateRoot -Recurse -Force
    }
}
finally {
    if (Test-Path -Path $stagingRoot) {
        Remove-Item -Path $stagingRoot -Recurse -Force
    }
}

Write-Host "Backup restored to data: $RestoreRoot"
Write-Host "Backup restored to signing-state: $SigningStateRoot"
