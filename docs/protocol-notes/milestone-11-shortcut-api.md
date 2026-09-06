# Milestone 11 Shortcut API Completion Notes

Scope covered in code and tests:

- Bearer token verification for Shortcut API endpoints.
- Token storage policy supports hash-only persistence with revoke support.
- `GET /api/shortcut/refresh-plan` implemented and returns at most one app according to priority policy.
- `POST /api/shortcut/prompted` implemented to persist prompt timestamp for cooldown control.
- Prompt cooldown and opportunity policy implemented in service layer:
  - cooldown default: 12h
  - opportunity window default: 72h
  - prompt is issued only when app is due and within opportunity window
- Priority policy implemented when multiple apps are due:
  - earlier `nextSignDueAt` first
  - then older `lastPromptAt`
  - then stable app id ordering
- Deploy-ready API behavior hardening:
  - `/api/shortcut/prompted` validates request payload (`appId`, `buildId`) and returns 400 for invalid input.
  - `/api/shortcut/prompted` returns 404 when app/build state cannot be matched.
  - `/api/shortcut/refresh-plan` returns 503 problem details when required runtime settings are invalid.
  - bootstrap token fallback can be disabled by leaving `ShortcutBootstrapToken` empty (default).

Implementation files:

- `src/SignManager.Web/Program.cs`
- `src/SignManager.Web/Services/ShortcutApiService.cs`
- `src/SignManager.Web/Services/ShortcutTokenStore.cs`
- `src/SignManager.Web/Services/ShortcutApiContracts.cs`
- `src/SignManager.Web/Services/WebUiOptions.cs`
- `src/SignManager.Web/appsettings.json`

Validation status:

- Integration tests added for:
  - bootstrap and hash token validation
  - bootstrap rejection when bootstrap token is not configured
  - token revoke behavior
  - refresh-plan single-app selection with priority policy
  - refresh-plan rejects overdue apps beyond opportunity window
  - refresh-plan fails fast when public base URL is not HTTPS
  - prompted timestamp persistence
  - cooldown suppression and post-cooldown re-eligibility
- Full solution tests are green.

Status:

- Milestone 11 is deploy-ready for shortcut API validation, opportunity gating, and error semantics.
- Milestone 12 operations and notification hardening can build on scheduler + web + shortcut flow.
