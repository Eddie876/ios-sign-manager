# Milestone 0 Protocol Research Completion Notes

This document records the research deliverables required before production-oriented implementation.

Deliverables completed in this repository:

- GrandSlam request/response behavior notes
- 2FA challenge and verification notes
- Developer Services endpoint behavior notes
- Deterministic fixture set used by Apple unit tests
- Anisette provider interface and required header contract

Research boundary:

- Splice is used as a behavior reference only.
- Notes are behavior/spec oriented and avoid source-level copying.
- Runtime code stays independently implemented in C#.

Related artifacts:

- `docs/protocol-notes/grandslam-request-response.md`
- `docs/protocol-notes/two-factor-flow.md`
- `docs/protocol-notes/developer-services-endpoints.md`
- `docs/protocol-notes/anisette-interface-contract.md`
- `tests/SignManager.Apple.Tests/Fixtures/GrandSlamFixtures.cs`
- `tests/SignManager.Apple.Tests/Fixtures/DeveloperFixtures.cs`
- `tests/SignManager.Apple.Tests/Fixtures/AnisetteFixtures.cs`
- `tests/SignManager.Apple.Tests/Fixtures/ProvisioningProfileFixture.cs`

Verification status:

- Fixture-backed unit tests validate mapping and failure paths for:
  - GrandSlam login response handling
  - 2FA-required response handling
  - Developer Services payload mapping
  - Anisette required-header validation
