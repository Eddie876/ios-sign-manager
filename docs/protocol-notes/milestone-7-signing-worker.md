# Milestone 7 Signing Worker Completion Notes

Scope covered in code and tests:

- End-to-end signing orchestration service (`SigningJobProcessor`) implemented in Worker layer.
- Single global signing semaphore (`GlobalSigningGate`) enforces one active signing flow at a time.
- Job state transitions and timeline now include:
  - preflight
  - provisioning
  - signing
  - validation
  - ready / failed
- Retry policy integration:
  - retryable failures map to delay windows
  - non-retryable failures stop retries
- Stable failure code mapping implemented for common signing failures:
  - `INVALID_IPA`
  - `AUTH_REQUIRED`
  - `ZSIGN_FAILED`
  - `SIGNED_IPA_VALIDATION_FAILED`
- Build validation safeguards added:
  - immutable source SHA256 must match app config and job input
  - signed output artifact must exist with valid size/hash

Validation status:

- `SignManager.Worker.Tests` added and included in solution.
- Worker tests cover success path, retryable signer failure, non-retryable auth failure, source SHA mismatch, and global semaphore exclusivity.
- Full solution test run is green.

Status:

- Milestone 7 is complete at fixture-test level.
- Milestone 8 scheduler implementation can now build on the stable worker signing orchestration contract.
