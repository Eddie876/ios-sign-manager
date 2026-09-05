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
- Milestone 6 implemented at fixture-test level and ready for Web/Worker integration.
