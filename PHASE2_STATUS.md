# Phase 2 Status

## Goal

Phase 2 enables real Microsoft Teams delivery through Microsoft Graph while keeping Phase 1 console simulation available.

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
- Startup validation prevents Graph mode from starting with incomplete settings.
- SQL connection string can be supplied with `MSSQL_CONNECTION_STRING`, matching the pattern used by `TestWebApp`.

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

Use .NET User Secrets for sensitive values. Do not commit real tenant, client, secret, or refresh token values.

Set sender and target users:

```powershell
dotnet user-secrets set "Teams:TenantId" "<tenant-id>" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:ClientId" "<client-id>" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:SenderUserEmail" "sender@your-domain.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:TargetUserEmail" "alvint@amulaire.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:AuthMode" "DeviceCode" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:SendMode" "Graph" --project .\TL_ORR\TL_ORR.csproj
```

On first Graph run, follow the device-code sign-in message printed in the logs. Sign in as the sender account.

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

- Insert or reset one `dbo.ProductIns` row with `CheckResult = 'NG'` and `IsSentTeams = 0`.
- Run:

```powershell
dotnet run --project .\TL_ORR\TL_ORR.csproj
```

- Confirm the target user receives the Teams message.
- Confirm the SQL row is updated:
  - `IsSentTeams = 1`
  - `SentTeamsTime` is not null
  - `SendErrorMessage = NULL`
