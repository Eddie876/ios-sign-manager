param(
    [string]$DataRoot = "./data",
    [string]$SigningStateRoot = "./signing-state",
    [string]$OutputPath = "./backups/sign-manager-backup.zip"
)

$ErrorActionPreference = "Stop"

$fullDataRoot = (Resolve-Path -Path $DataRoot).Path
$fullSigningStateRoot = $null
if (Test-Path -Path $SigningStateRoot) {
    $fullSigningStateRoot = (Resolve-Path -Path $SigningStateRoot).Path
}
$outputDir = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if (Test-Path -Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sign-manager-backup-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

try {
    $stagingData = Join-Path $stagingRoot "data"
    New-Item -ItemType Directory -Path $stagingData -Force | Out-Null
    Copy-Item -Path (Join-Path $fullDataRoot "*") -Destination $stagingData -Recurse -Force

    if (-not [string]::IsNullOrWhiteSpace($fullSigningStateRoot)) {
        $stagingSigningState = Join-Path $stagingRoot "signing-state"
        New-Item -ItemType Directory -Path $stagingSigningState -Force | Out-Null
        Copy-Item -Path (Join-Path $fullSigningStateRoot "*") -Destination $stagingSigningState -Recurse -Force
    }

    Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $OutputPath -CompressionLevel Optimal
}
finally {
    if (Test-Path -Path $stagingRoot) {
        Remove-Item -Path $stagingRoot -Recurse -Force
    }
}

Write-Host "Backup created at $OutputPath"
