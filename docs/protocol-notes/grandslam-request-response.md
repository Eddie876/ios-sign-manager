# GrandSlam Request and Response Notes

Scope:

- Authentication protocol shape used by the current C# client
- Deterministic fixture responses used by unit tests
- Error-code mapping used by higher layers

Current client behavior contract:

- Request path: `POST /grandslam/login`
- Request payload fields:
  - `appleId`
  - `password`
- 2FA follow-up path: `POST /grandslam/2fa`
- 2FA payload fields:
  - `appleId`
  - `code`

Response mapping rules in current implementation:

- `status = ok`
  - requires non-empty `adsId`
  - requires non-empty `gsToken`
  - maps to successful `AppleSession`
- `status = 2fa_required`
  - maps to `RequiresTwoFactor = true`
- other status
  - maps to failure
  - uses `errorCode` if present
  - falls back to `APPLE_LOGIN_FAILED`

Validation notes:

- Missing `adsId` or `gsToken` on `ok` response is treated as session rejection.
- Mapping behavior is fixture-locked by unit tests.

References in code:

- `src/SignManager.Apple/Auth/GrandSlamHttpClient.cs`
- `src/SignManager.Apple/Auth/AppleAuthenticationService.cs`
- `tests/SignManager.Apple.Tests/GrandSlamHttpClientTests.cs`
- `tests/SignManager.Apple.Tests/Fixtures/GrandSlamFixtures.cs`
