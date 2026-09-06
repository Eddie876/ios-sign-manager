# Milestone 1 Auth Spike Completion Notes

Scope covered in code and tests:

- Anisette HTTP provider with required-header validation and retry
- SRP helper primitives (hash/HMAC/x/u/k helpers)
- GrandSlam HTTP client mapping (`login`, `2fa`) with anisette header injection
- 2FA-required flow in authentication service
- Session token persistence via encrypted store (`secrets.enc` envelope)
- Session restore behavior and invalid-session clearing
- Developer session validation via `viewDeveloper`
- SPD decrypt primitive (`AppleSpdDecryptor`) with deterministic test fixture
- CLI authentication entrypoint for operations:
	- `dotnet /app/cli/SignManager.Cli.dll apple login`
	- `dotnet /app/cli/SignManager.Cli.dll apple restore`

Deploy-ready updates:

- Added `SignManager.Cli` project to support operator-driven login and restore checks.
- CLI supports env-based wiring for runtime paths and endpoints:
	- `SIGNMANAGER_ANISETTE_BASE_URL`
	- `SIGNMANAGER_APPLE_GRANDSLAM_BASE_URL`
	- `SIGNMANAGER_APPLE_DEVELOPER_BASE_URL`
	- `SIGNMANAGER_SIGNING_STATE_PATH`
	- `SIGNMANAGER_MASTER_KEY_PATH`
- Master key bootstrap is blocked for `/run/secrets/*` paths to prevent accidental insecure key creation in containerized production.

Current status:

- Milestone 1 test coverage remains fixture-driven and deterministic.
- Runtime command path for secure session persistence and restore is now wired for container operations.
- No password persistence is implemented.
