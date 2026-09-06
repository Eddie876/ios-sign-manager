# Developer Services Endpoint Behavior Notes

Scope:

- Endpoint groups required by provisioning workflow
- Payload shape expectations used by client mapping tests

Implemented endpoint groups in current client:

- `GET /developer/view`
  - expected response field: `valid` (bool)
- `GET /developer/teams`
  - expected response field: `teams` (array)
  - each team: `teamId`, `name`
- `POST /developer/devices/ensure`
  - request: `teamId`, `udid`, `deviceName`
  - response: `deviceId`, `udid`, `name`
- `POST /developer/certificates/ensure`
  - request: `teamId`, `csrPem`
  - response: `certificateId`, `serialNumber`, `pem`, `expiresAt`
- `POST /developer/appids/ensure`
  - request: `teamId`, `bundleId`
  - response: `appIdId`, `bundleId`, `name`
- `POST /developer/profiles/create`
  - request: `teamId`, `appIdId`, `deviceId`, `profileName`
  - response: `profileId`, `uuid`, `creationDate`, `expirationDate`, `teamId`, `bundleId`, `mobileProvisionBase64`

Mapping and orchestration usage:

- `HttpAppleDeveloperClient` maps endpoint payloads into domain records.
- `AppleProvisioningService` composes ensure-device, CSR, certificate, app-id, and profile creation into one provisioning workflow.

Fixture references:

- `tests/SignManager.Apple.Tests/Fixtures/DeveloperFixtures.cs`
- `tests/SignManager.Apple.Tests/HttpAppleDeveloperClientTests.cs`
- `tests/SignManager.Apple.Tests/AppleProvisioningServiceTests.cs`
