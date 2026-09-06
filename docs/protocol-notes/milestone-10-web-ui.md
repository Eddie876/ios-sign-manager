# Milestone 10 Web UI Completion Notes

Scope covered in code and tests:

- Razor Pages Web UI now includes:
  - dashboard
  - app list
  - add app
  - replace IPA
  - Sign Now action
  - Install action URL rendering
  - Apple account status
  - settings
  - build history
- Web service layer (`WebAppService`) added to centralize data access and actions:
  - reads/writes `apps.json` and `state.json`
  - handles IPA upload temp persistence and cleanup
  - performs source replace through `SourceIpaManager`
  - updates runtime state for Sign Now
- Add App behavior displays required metadata after add:
  - Name
  - Version
  - Source Bundle ID
  - Effective Bundle ID
  - Extensions
  - Estimated App IDs used
  - Unsupported entitlements
  - Warnings
- Replace IPA behavior updates source hash and metadata while preserving app identity/schedule/publish settings.
- Apple Account page presents masked account and session/certificate statuses from settings with CLI login command guidance.

Implementation files:

- `src/SignManager.Web/Pages/Index.cshtml` (+ code-behind)
- `src/SignManager.Web/Pages/Apps/*`
- `src/SignManager.Web/Pages/Builds/Index.cshtml` (+ code-behind)
- `src/SignManager.Web/Pages/Apple/Status.cshtml` (+ code-behind)
- `src/SignManager.Web/Pages/Settings/Index.cshtml` (+ code-behind)
- `src/SignManager.Web/Services/*`
- `src/SignManager.Web/Program.cs`

Validation status:

- Integration tests added for core Web UI service flows:
  - add app persists config/state and returns metadata
  - Sign Now updates due state
  - dashboard returns Apple status and app counters
- Full solution tests are green.

Status:

- Milestone 10 is complete at fixture-test level.
- Milestone 11 Shortcut API can now build on the same app/state/settings service foundations.
