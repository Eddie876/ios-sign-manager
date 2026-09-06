# Milestone 3 Signing Spike Completion Notes

Scope covered in code and tests:

- Pinned zsign build stage added to Docker image build.
- zsign process invocation uses `ProcessStartInfo.ArgumentList` through `IProcessRunner`.
- Local process runner includes timeout handling and process-tree kill on timeout.
- Signing orchestration service performs:
  - zsign argument construction
  - process execution
  - output existence checks
  - signed IPA validation
  - SHA-256 output hashing
- Signed IPA validation checks:
  - `Payload/*.app/Info.plist` exists
  - `embedded.mobileprovision` exists
  - bundle ID matches expected
  - profile UUID and expiration match expected

Deploy-ready updates:

- zsign signing service now validates inputs before process start:
  - source IPA exists
  - private key/certificate/profile files exist
  - timeout and output-byte limits are positive
  - configured executable path is validated when explicit path is provided
- Worker supports `SIGNMANAGER_ZSIGN_PATH` runtime override for deterministic deployment paths.
- Added failure-path tests for timeout, missing source IPA, and missing output artifact after successful process exit.

Current constraints:

- Fixture-level validation currently focuses on XML Info.plist test artifacts.
- Live zsign binary behavior and real IPA edge cases remain in Spike C live verification scope.
