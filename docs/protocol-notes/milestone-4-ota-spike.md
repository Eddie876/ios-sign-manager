# Milestone 4 OTA Spike Completion Notes

Scope covered in code and tests:

- OTA manifest generator produces Apple plist payload with package URL and metadata.
- `itms-services://` install URL builder implemented with encoded manifest URL.
- R2 object key planner implemented for immutable build paths and latest pointers.
- Publish-order key list follows immutable artifacts first, latest pointers last.

Deploy-ready updates:

- Worker publish flow now uses configurable `OtaPublicBaseUrl` instead of placeholder URL.
- Runtime env override supported via `SIGNMANAGER_PUBLIC_BASE_URL`.
- OTA publish path validates `OtaPublicBaseUrl` as absolute HTTPS URL before R2 publish.
- Added worker tests for configured URL usage and invalid-URL failure behavior.

Implemented as fixture-level spike validation:

- Test verification for manifest structure and key fields
- Test verification for install URL encoding
- Test verification for R2 path layout and namespace generation

Out of scope for this repository-only spike pass:

- Real-device install runtime verification
- Wake alarm / charger trigger runtime verification
- Live Cloudflare R2 object upload integration
