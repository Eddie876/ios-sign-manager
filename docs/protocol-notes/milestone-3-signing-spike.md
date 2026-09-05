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

Current constraints:

- Fixture-level validation currently focuses on XML Info.plist test artifacts.
- Live zsign binary behavior and real IPA edge cases remain in Spike C live verification scope.
