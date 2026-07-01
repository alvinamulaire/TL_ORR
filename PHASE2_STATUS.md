# Phase 2 Status

## Goal

Phase 2 enables real Microsoft Teams delivery through Microsoft Graph while keeping Phase 1 console simulation available.

## Current Status

Phase 2 implementation is complete. The remaining acceptance action is the controlled real-send Graph test, which should be run only when you are ready for the target user to receive a Teams message.

## Important Graph Constraint

Normal `POST /chats/{chat-id}/messages` delivery requires delegated Microsoft Graph permissions. After reviewing `AutomateWork`, TL_ORR now uses the same device-code delegated flow with persistent token cache.

## Implemented

- `Teams:SendMode = Console` keeps Phase 1 simulation.
- `Teams:SendMode = Graph` enables real Microsoft Graph delivery.
- Graph mode requires:
  - `Teams:AuthMode = DeviceCode`
  - `Teams:TenantId`
  - `Teams:ClientId`
  - `Teams:TokenCacheName`
  - `Teams:DelegatedScopes`
  - `Teams:SenderUserEmail`
  - `Teams:TargetUserEmail`
- Device-code delegated sign-in with persistent token cache.
- The worker resolves sender and target users by email.
- The worker creates or returns a one-on-one chat.
- The worker sends the formatted HTML notification message to the chat.
- The notification includes the image path. Inline preview is supported for HTTP/HTTPS image URLs.
- Graph mode logs sender/target resolution and chat creation progress for acceptance troubleshooting.
- Startup validation prevents Graph mode from starting with incomplete settings.
- SQL connection string can be supplied with `MSSQL_CONNECTION_STRING`, matching the pattern used by `TestWebApp`.
- Helper scripts can enable Graph mode, disable Graph mode, and run one Phase 2 Graph acceptance cycle.
- The Phase 2 acceptance script now generates a unique test SFC and verifies the SQL sent status after the worker exits.
- During acceptance, `Worker:TestSfcFilter` limits the Graph run to the generated test row.
- No-send Phase 2 preflight checks are available through `scripts/test-phase2-preflight.ps1`.
- Windows Service deployment environment variables can be configured through `scripts/set-windows-service-env.ps1`.
- Windows Service installation supports a dedicated service account through `scripts/install-windows-service.ps1 -Credential`.

## TestWebApp Reference Check

`C:\Users\alvint\source\repos\TestWebApp` was inspected for Teams/Graph sending code and settings. No Teams or Microsoft Graph message-sending implementation was found in the source project. The reusable integration pattern found there is SQL configuration precedence:

1. `MSSQL_CONNECTION_STRING` environment variable.
2. `ConnectionStrings:DefaultConnection` from configuration.

TL_ORR now follows the same SQL connection precedence.

## AmulaireService Reference Check

`C:\Users\alvint\source\repos\AmulaireService` includes an existing internal notification integration:

- API URL setting: `SendMailApiUrl`
- API key header: `X-Api-Key`
- DTO: `SendMailDto`
- HTTP pattern: `POST` JSON to the configured API URL with `X-Api-Key`

This API is a mail notification API rather than Microsoft Graph Teams chat. TL_ORR now supports it through:

- `Teams:SendMode = AmulaireMailApi`
- `Teams:MailApiUrl`
- `Teams:MailApiKey`
- `Teams:TargetUserEmail`
- `Teams:CcTo`

The API URL and key were saved locally with .NET User Secrets and are not committed to Git.

## AutomateWork Reference Check

`C:\Users\alvint\source\repos\AutomateWork` includes the best matching Teams direct message implementation:

- `TeamsDirectMessageNotificationSender`
- `TeamsNotifyOptions`
- `Microsoft.Graph` SDK
- `Azure.Identity.DeviceCodeCredential`
- `TokenCachePersistenceOptions`
- delegated scopes:
  - `ChatMessage.Send`
  - `Chat.ReadWrite`
  - `Chat.Create`
  - `User.Read`
  - `User.ReadBasic.All`
  - `offline_access`

TL_ORR Graph mode has been aligned to this pattern.

## Required Entra App Setup

Create or update an Azure Entra App Registration:

- Public client/device-code flow enabled if required by tenant policy.
- Delegated Microsoft Graph permissions:
  - `User.Read`
  - `User.ReadBasic.All`
  - `Chat.Create`
  - `Chat.ReadWrite`
  - `ChatMessage.Send`
  - `offline_access`
- Grant admin consent if required by tenant policy.

## Local Secret Setup

Use .NET User Secrets for sensitive values. Do not commit real tenant or client values.

Set sender and target users:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\set-teams-graph-devicecode-secrets.ps1 `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -SenderUserEmail "sender@your-domain.com" `
  -TargetUserEmail "alvint@amulaire.com"
```

Enable Graph mode only when you are ready to send real Teams messages:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\enable-teams-graph-mode.ps1
```

On first Graph run, follow the device-code sign-in message printed in the logs. Sign in as the sender account.

Return to Console mode after a real-send test:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\disable-teams-graph-mode.ps1
```

For deployment, use environment variables:

```powershell
$env:MSSQL_CONNECTION_STRING = "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
$env:Teams__SendMode = "Graph"
$env:Teams__AuthMode = "DeviceCode"
$env:Teams__TenantId = "<tenant-id>"
$env:Teams__ClientId = "<client-id>"
$env:Teams__TokenCacheName = "TL-ORR-Teams-Delegated"
$env:Teams__SenderUserEmail = "sender@your-domain.com"
$env:Teams__TargetUserEmail = "alvint@amulaire.com"
```

## Phase 2 Acceptance

- Run the no-send preflight:

```powershell
$env:TL_ORR_SQL_PASSWORD = "<password>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-phase2-preflight.ps1
```

- Insert or reset one `dbo.ProductIns` row with `CheckResult = 'NG'` and `IsSentTeams = 0`.
- Suggested script: `database/004_insert_productins_phase2_graph_sample.sql`. Pass SQLCMD variable `Sfc` when running it directly.
- Run:

```powershell
$env:TL_ORR_SQL_PASSWORD = "<password>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-phase2-graph-acceptance.ps1
```

- Confirm the target user receives the Teams message.
- Confirm the SQL row is updated:
  - `IsSentTeams = 1`
  - `SentTeamsTime` is not null
  - `SendErrorMessage = NULL`

## Completion Summary

- Graph DeviceCode Teams direct message delivery is implemented.
- Console simulation remains available as the safe default.
- Amulaire Mail API mode is available as an alternate notification channel.
- SQL sent/failed state handling is implemented.
- One-row Graph acceptance is isolated with `Worker:TestSfcFilter`.
- No-send preflight is available before real delivery.
- Windows Service deployment scripts and environment variable setup are available.
