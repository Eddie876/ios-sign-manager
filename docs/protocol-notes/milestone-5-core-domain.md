# Milestone 5 Core Domain Completion Notes

Scope covered in code and tests:

- Versioned persistence stores added for:
  - `apps.json` (`AppConfigStore`)
  - `state.json` (`AppStateStore`)
- Schema version enforcement implemented with explicit unsupported-version exception.
- Atomic file persistence integrated through `JsonAtomicFileStore<T>` in both stores.
- Domain mapping in/out of storage documents established for AppConfig and AppState models.
- Integration tests validate:
  - AppConfig roundtrip with version field
  - AppState roundtrip with runtime status fields
  - unsupported schema version rejection
  - atomic overwrite behavior

Deploy-ready updates:

- `AppConfigStore` now validates required fields before returning data to runtime:
  - non-empty app id/name/source/bundle id/slug
  - positive `schedule.intervalHours`
  - duplicate app-id rejection
  - publish slug pattern enforcement (`[a-z0-9-]+`)
- `AppStateStore` now validates state timeline consistency:
  - reject `profileExpirationDate < profileCreationDate`
  - reject `lastSuccessfulSignAt > profileExpirationDate`
- `JsonAtomicFileStore<T>` now forces file-system flush before atomic replace/move to reduce crash-window data loss.
- Added milestone tests for malformed persistence inputs (duplicate app id and invalid profile date ordering).

Status:

- Milestone 5 is complete at deploy-ready persistence level.
- Next milestones can depend on stable `apps.json` / `state.json` contracts.
