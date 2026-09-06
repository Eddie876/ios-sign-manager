# Milestone 12 Operations Completion Notes

Scope covered in code and tests:

- Telegram exception alert plumbing added:
  - non-retryable signing failures trigger alerts
  - scheduler loop exceptions trigger alerts
- Health checks added to web host:
  - `/health/live`
  - `/health/ready`
- Structured JSON logs enabled in web and worker host startup.
- Backup and restore operation support:
  - `DataBackupService` for archive backup and restore flows
  - integration test validates roundtrip restoration
  - backup rejects output paths inside data root to avoid self-inclusion/corruption
  - restore enforces robust root-bound path validation and rejects traversal entries
- Docker hardening applied:
  - non-root runtime user
  - read-only root filesystem in compose
  - no-new-privileges
  - all Linux capabilities dropped
  - tmpfs mount for `/tmp`
- Cleanup support added:
  - worker job-workspace cleanup service with max age policy
  - cleanup isolates per-directory delete failures and continues processing
  - manual cleanup script for operations
  - worker test validates old-directory cleanup behavior
- Deployment documentation expanded with runbook-level steps.

Implementation files:

- `src/SignManager.Infrastructure/Notifications/*`
- `src/SignManager.Infrastructure/Operations/DataBackupService.cs`
- `src/SignManager.Worker/Operations/JobWorkspaceCleanupService.cs`
- `src/SignManager.Worker/Worker.cs`
- `src/SignManager.Worker/Program.cs`
- `src/SignManager.Web/Program.cs`
- `docker/Dockerfile`
- `docker/docker-compose.yml`
- `scripts/backup.ps1`
- `scripts/restore.ps1`
- `scripts/cleanup-jobs.ps1`
- `docs/deployment.md`

Validation status:

- Integration tests include backup/restore roundtrip.
- Integration tests include backup output-location guard and restore traversal rejection.
- Worker tests include cleanup behavior, per-directory delete-failure isolation, and non-retryable failure alert emission.
- Full solution tests are green.

Status:

- Milestone 12 is deploy-ready for operations path safety and cleanup fault isolation.
- Current baseline now includes scheduler, signing, publish, web UI, shortcut API, and operations hardening foundations.
