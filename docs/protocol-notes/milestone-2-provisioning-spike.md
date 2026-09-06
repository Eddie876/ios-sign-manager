# Milestone 2 Provisioning Spike Completion Notes

Scope covered in code and tests:

- `IAppleDeveloperClient` expanded for provisioning workflow operations:
  - team listing
  - ensure device
  - ensure certificate
  - ensure app id
  - create profile
- HTTP developer client mappings for all above endpoint categories
- Native RSA 2048 + PKCS#10 CSR generation with encrypted PKCS#8 private key export
- Provisioning profile parser for UUID / dates / team / bundle id / device list extraction
- Provisioning orchestration service that validates profile freshness against previous profile state
- Worker runtime wiring now uses concrete provisioning provider:
  - restores encrypted Apple session before provisioning
  - resolves team from config or `GetTeams`
  - generates fresh profile per app and validates freshness
  - writes profile/certificate/private key material to job workspace for signing
  - persists encrypted key and provisioning metadata under signing-state

Deploy-ready updates:

- `UnavailableProvisioningMaterialProvider` placeholder replaced with `AppleProvisioningMaterialProvider` in worker DI.
- Worker scheduler options now include provisioning runtime config (device/team/session/endpoint paths).
- Added worker tests for provisioning material generation and auth-required short-circuit.

Verification status:

- All Milestone 2 components validated with deterministic unit tests.
- Profile freshness check uses `ProfileFreshnessPolicy` from Core.
- Worker provisioning integration is validated through unit tests with fake Apple endpoints.
- Live Apple Developer Services execution still requires environment-specific endpoint credentials and runtime verification.
