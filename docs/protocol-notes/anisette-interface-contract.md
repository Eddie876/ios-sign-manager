# Anisette Interface Contract Notes

Interface in code:

- `IAnisetteProvider.GetHeadersAsync(CancellationToken)`
- Return type: `AnisetteHeaders`

Required anisette keys:

- `X-Apple-I-MD`
- `X-Apple-I-MD-M`
- `X-Apple-I-MD-LU`
- `X-Mme-Device-Id`
- `X-Mme-Client-Info`

Current HTTP provider behavior:

- endpoint path default: `headers`
- performs up to 3 attempts
- retry backoff: 200ms * attempt index
- validates all required keys are present and non-empty
- throws on missing/invalid response

Security boundary notes:

- Anisette server only provides headers.
- Apple password, session token, private key, and signing artifacts must not be forwarded to anisette provider endpoints.

References in code:

- `src/SignManager.Apple/Anisette/IAnisetteProvider.cs`
- `src/SignManager.Apple/Anisette/AnisetteHeaders.cs`
- `src/SignManager.Apple/Anisette/HttpAnisetteProvider.cs`
- `tests/SignManager.Apple.Tests/AnisetteHeaderTests.cs`
