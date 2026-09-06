# Milestone 6 IPA Management Completion Notes

Scope covered in code and tests:

- Safe IPA preflight service (`IpaPreflightService`) with checks for:
  - max upload bytes
  - max entry count
  - max expanded bytes
  - max single-entry bytes
  - max compression ratio
  - path traversal and absolute paths
  - symlink rejection
  - single main app requirement under `Payload/*.app`
- Metadata extraction from IPA:
  - app name
  - source bundle id
  - version/build
  - minimum iOS version
  - extensions (`PlugIns/*.appex`)
  - entitlement keys
  - watch/app clips presence and warnings
- Source replacement and immutability workflow (`SourceIpaManager`):
  - preflight before replace
  - atomic replace of immutable source path
  - per-job copy from immutable source

Validation status:

- Unit tests cover valid IPA metadata extraction and key rejection paths.
- Unit tests cover immutable source replace and job copy behavior.

Deploy-ready updates:

- `SourceIpaManager` now serializes replace/copy operations via in-process lock to avoid concurrent read/replace races.
- Source replace now enforces durability and integrity:
  - flush temp file to disk before atomic replace/move
  - verify final immutable file SHA-256 matches preflight hash
  - guaranteed temp-file cleanup on failure
- Web upload flow now uses configurable IPA limits and source root path (`WebUiOptions`), removing hardcoded source directory.
- Added early upload-size rejection in Web layer to reduce oversized temp-file writes.
- Expanded tests for absolute-entry ZIP rejection and upload-size limit failures.

- Milestone 6 is complete at deploy-ready level.
