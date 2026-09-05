# ios-sign-manager

Self-hosted iOS personal-team signing and OTA refresh platform implemented in C#.

## Current status

This repository is initialized with the planned solution structure and early baseline code for:

- Core domain models and scheduling/freshness policies
- Apple anisette provider abstraction
- zsign argument builder wrapper
- Atomic JSON persistence primitive
- Unit test scaffolding for the above

Milestone progress snapshot:

- Milestone 0 (protocol note scaffolding): in progress
- Milestone 1 (authentication spike): completed at fixture-test level
- Milestone 2+: not started

## Solution layout

- `src/SignManager.Web` Razor Pages + API host
- `src/SignManager.Worker` background scheduler host
- `src/SignManager.Core` pure domain models and logic
- `src/SignManager.Apple` Apple auth and developer-service client abstractions
- `src/SignManager.Signing` signing pipeline and zsign integration
- `src/SignManager.Infrastructure` persistence, process, encryption, external adapters
- `tests/*` unit and integration tests

## Build

```bash
dotnet restore
dotnet build ios-sign-manager.slnx
```

## Test

```bash
dotnet test ios-sign-manager.slnx
```

## Milestone discipline

Implementation follows `ios-sign-manager-csharp-development-plan.md` in order:

1. Spike A: Apple login + 2FA + token restore
2. Spike B: Developer provisioning
3. Spike C: zsign signing
4. Spike D: OTA + shortcut

Only after all spikes pass do we proceed with complete web feature implementation.
