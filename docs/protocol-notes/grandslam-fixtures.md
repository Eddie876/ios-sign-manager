# GrandSlam Fixture Notes (Milestone 1)

These deterministic fixtures are used to lock response mapping behavior while the real protocol implementation evolves.

## Login success fixture

```json
{
  "status": "ok",
  "adsId": "123456789",
  "gsToken": "gs-token-value"
}
```

Expected mapping:

- `Success = true`
- `RequiresTwoFactor = false`
- session contains `adsId` and `gsToken`

## Two-factor required fixture

```json
{
  "status": "2fa_required"
}
```

Expected mapping:

- `Success = false`
- `RequiresTwoFactor = true`

## Failure fixture

```json
{
  "status": "error",
  "errorCode": "APPLE_LOGIN_FAILED"
}
```

Expected mapping:

- `Success = false`
- `ErrorCode = APPLE_LOGIN_FAILED`

## Current endpoint assumptions

- `POST /grandslam/login`
- `POST /grandslam/2fa`

These endpoint paths are placeholders for Milestone 1 spike testing and will be replaced once live protocol traces are validated.
