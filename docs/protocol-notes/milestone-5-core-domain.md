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

Status:

- Milestone 5 is complete at fixture/integration-test level.
- Next milestones can depend on stable `apps.json` / `state.json` contracts.
