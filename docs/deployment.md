# Deployment Notes

## Runtime topology

- `sign-manager` container:
	- ASP.NET Core Web UI
	- Shortcut API (`/api/shortcut/*`)
	- Background Worker scheduler
	- zsign binary for signing flows
- `anisette` container: anisette headers provider

## Data volumes

- `/data`: config, state, sources, jobs
- `/signing-state`: encrypted credentials and signing state

## Health checks

- Web liveness endpoint: `GET /health/live`
- Web readiness endpoint: `GET /health/ready`
- Docker health check probes `http://127.0.0.1:8080/health/live`

## Structured logs

- Web and Worker both use JSON console logging.
- Log scope and timestamp are enabled by default for machine parsing.

## Telegram exception alerts

Worker supports exception alerts for:

- non-retryable signing failures
- scheduler loop failures

Enable in `src/SignManager.Worker/appsettings.json` under `Scheduler`:

```json
{
	"Scheduler": {
		"TelegramAlertsEnabled": true,
		"TelegramBotToken": "<bot-token>",
		"TelegramChatId": "<chat-id>"
	}
}
```

## Apple login operations

Initial login and token refresh are performed via CLI inside container:

```powershell
docker compose -f docker/docker-compose.yml exec sign-manager dotnet /app/cli/SignManager.Cli.dll apple login
docker compose -f docker/docker-compose.yml exec sign-manager dotnet /app/cli/SignManager.Cli.dll apple restore
```

Related environment variables:

- `SIGNMANAGER_ANISETTE_BASE_URL`
- `SIGNMANAGER_APPLE_GRANDSLAM_BASE_URL`
- `SIGNMANAGER_APPLE_DEVELOPER_BASE_URL`
- `SIGNMANAGER_SIGNING_STATE_PATH`
- `SIGNMANAGER_MASTER_KEY_PATH`

## Worker provisioning configuration

Set the following `Scheduler` values (via appsettings or mapped env strategy):

- `AppleDeviceUdid`: target iPhone UDID
- `AppleDeviceName`: display name used for ensure-device
- `AppleTeamId`: optional fixed team id (when empty, first returned team is used)
- `ApplePrivateKeyPassword`: password used for encrypted PKCS#8 key handling
- `ProfileMinimumFreshHours`: profile freshness lower bound (default 144)
- `AppleSessionSecretsPath`: encrypted Apple session token file
- `AppleSessionMasterKeyPath`: session master key file path

## zsign execution configuration

- `Scheduler.ZsignExecutablePath` default is `zsign`.
- For deterministic container execution, set `SIGNMANAGER_ZSIGN_PATH=/usr/local/bin/zsign`.
- Startup checks in signing path now validate required source/profile/certificate/key files before invoking zsign.

## OTA public URL configuration

- Worker publish flow requires `Scheduler.OtaPublicBaseUrl` as absolute HTTPS URL.
- Environment override: `SIGNMANAGER_PUBLIC_BASE_URL`.
- Example: `https://ios.example.com`

Current worker defaults are defined in:

- `src/SignManager.Worker/appsettings.json`

## Web IPA management configuration

- Source IPA root directory is configurable via `WebUi.SourceRootDirectory` (default `data/sources`).
- Upload and preflight safety limits are configurable via `WebUi`:
	- `UploadMaxBytes`
	- `UploadMaxEntries`
	- `UploadMaxTotalExpandedBytes`
	- `UploadMaxSingleEntryBytes`
	- `UploadMaxCompressionRatio`
- Web rejects uploads that exceed `UploadMaxBytes` before temp-file write.

Current web defaults are defined in:

- `src/SignManager.Web/appsettings.json`

## Backup and restore

Local scripts:

- Backup: `scripts/backup.ps1`
- Restore: `scripts/restore.ps1`

Examples:

```powershell
./scripts/backup.ps1 -DataRoot ./data -OutputPath ./backups/sign-manager-backup.zip
./scripts/restore.ps1 -BackupPath ./backups/sign-manager-backup.zip -RestoreRoot ./data-restore
```

## Workspace cleanup

- Worker periodically cleans old job directories under `WorkspaceRoot`.
- Controlled by `Scheduler.CleanupMaxAgeHours` (default 72h).

Manual cleanup script:

```powershell
./scripts/cleanup-jobs.ps1 -WorkspaceRoot ./data/jobs -MaxAgeHours 72
```

## Docker hardening

Container hardening defaults include:

- non-root runtime user (`appuser`, uid 10001)
- read-only root filesystem
- `no-new-privileges`
- dropped Linux capabilities (`ALL`)
- tmpfs mount on `/tmp`

## Deploy checklist

1. Set `.env` values.
2. Configure `Scheduler` section for paths and alert settings.
3. Start stack: `docker compose -f docker/docker-compose.yml up -d --build`.
4. Verify health: `curl http://localhost:8080/health/live`.
5. Run first backup and verify restore path.
