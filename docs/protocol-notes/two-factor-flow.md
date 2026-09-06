# Apple 2FA Flow Notes

Scope:

- Service-level behavior for login plus 2FA
- Error-code behavior expected by scheduler and UI layers

Current flow:

1. Call login endpoint with Apple ID and password.
2. If response is `2fa_required`:
   - if caller has no 2FA code, return `APPLE_2FA_REQUIRED`
   - if caller provides 2FA code, call 2FA endpoint
3. If 2FA verification succeeds and session validates against Developer Services, persist session token.

Session validation requirement:

- Authentication is not considered complete until `viewDeveloper` indicates a valid developer session.

Error behavior:

- missing 2FA code when required -> `APPLE_2FA_REQUIRED`
- login failure response -> mapped error code or `APPLE_LOGIN_FAILED`
- invalid developer session after login -> `APPLE_SESSION_REJECTED`

Security notes:

- Password is not persisted by the implemented storage model.
- Session persistence is encrypted and versioned through the session envelope.

References in code:

- `src/SignManager.Apple/Auth/AppleAuthenticationService.cs`
- `src/SignManager.Apple/Auth/GrandSlamHttpClient.cs`
- `src/SignManager.Apple/Auth/EncryptedAppleSessionStore.cs`
- `tests/SignManager.Apple.Tests/AppleAuthenticationServiceTests.cs`
