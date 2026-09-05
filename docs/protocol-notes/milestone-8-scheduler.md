# Milestone 8 Scheduler Completion Notes

Scope covered in code and tests:

- Scheduler scan loop implemented in Worker host and executed on interval.
- 48-hour due scheduling behavior is covered through `AppScheduleConfig.IntervalHours` + `RefreshPlanner` based checks.
- Idempotency behavior implemented:
  - apps in `Pending` state are skipped
  - per scan, each app is processed at most once
- Retry behavior implemented:
  - scheduler persists retry due timestamp from signing result (`NextRetryAt`) into runtime state
  - subsequent scans respect due-time gating
- Auth-required behavior implemented:
  - auto-scan does not trigger when app runtime status is `AuthRequired`
- Manual Sign Now behavior implemented:
  - in-memory trigger store allows explicit manual request per app
  - manual trigger can run even while app is in `AuthRequired`

Implementation files:

- `src/SignManager.Worker/Worker.cs`
- `src/SignManager.Worker/Program.cs`
- `src/SignManager.Worker/Signing/WorkerSigningScheduler.cs`
- `src/SignManager.Worker/Signing/ManualSignTriggerStore.cs`
- `src/SignManager.Worker/Signing/SigningContracts.cs`
- `src/SignManager.Worker/Signing/UnavailableProvisioningMaterialProvider.cs`

Validation status:

- New scheduler tests cover due trigger, auth-required skip, manual override, retry persistence, and pending-state idempotency.
- Full solution tests are green.

Status:

- Milestone 8 is complete at fixture-test level.
- Milestone 9 can now build on stable scheduler-triggered signing orchestration.
