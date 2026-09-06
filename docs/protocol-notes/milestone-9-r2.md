# Milestone 9 R2 Completion Notes

Scope covered in code and tests:

- Immutable versioned object path publishing is implemented using `R2ObjectKeyPlanner` + `R2ReleasePublisher`.
- Latest pointer artifacts are published (`latest/manifest.plist`, `latest/latest.json`) after versioned artifacts.
- OTA manifest generation is integrated into publish flow.
- MIME content types are explicit:
  - IPA: `application/octet-stream`
  - manifest: `application/x-plist`
  - metadata JSON: `application/json`
- Partial failure handling is implemented:
  - publish tracks upload progress
  - if a later upload fails, previously uploaded objects are rolled back via delete calls
- Cleanup behavior is covered through best-effort rollback of partial uploads.

Worker integration:

- `SigningJobProcessor` now executes `Publishing` stage through `IBuildPublisher` after validation.
- `R2BuildPublisher` bridges worker build output to `R2ReleasePublisher` and preserves stable failure codes.

Validation status:

- Integration tests added for:
  - immutable + latest publish objects and MIME checks
  - partial failure rollback cleanup
- Full solution tests are green.

Deploy-ready updates:

- `R2ReleasePublisher` now validates signed IPA metadata consistency before publish:
  - `SizeBytes` must match on-disk file length
  - `Sha256` must match on-disk file hash
- Added explicit versioned-artifact verification gate (`ObjectExistsAsync`) before writing latest pointers.
- If verification fails, publish aborts and rolls back uploaded versioned objects; latest pointers are never updated.
- Integration tests now cover verification-gate failure path and latest-pointer safety.

Status:

- Milestone 9 is complete at deploy-ready level.
- Milestone 10 Web UI can now build on scheduler + signing + publish pipeline foundations.
