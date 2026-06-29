# Phase 2 Status

## Goal

Phase 2 enables real Microsoft Teams delivery through Microsoft Graph while keeping Phase 1 console simulation available.

## Important Graph Constraint

Normal `POST /chats/{chat-id}/messages` delivery requires delegated Microsoft Graph permissions. The worker therefore uses `Teams:AuthMode = DelegatedRefreshToken` for real Teams sending.

## Implemented

- `Teams:SendMode = Console` keeps Phase 1 simulation.
- `Teams:SendMode = Graph` enables real Microsoft Graph delivery.
- Graph mode requires:
  - `Teams:AuthMode = DelegatedRefreshToken`
  - `Teams:TenantId`
  - `Teams:ClientId`
  - `Teams:ClientSecret`
  - `Teams:RefreshToken`
  - `Teams:SenderUserEmail`
  - `Teams:TargetUserEmail`
- Refresh token flow exchanges `Teams:RefreshToken` for an access token.
- The worker resolves sender and target users by email.
- The worker creates or returns a one-on-one chat.
- The worker sends the formatted HTML notification message to the chat.
- Startup validation prevents Graph mode from starting with incomplete settings.
- Helper scripts are available:
  - `scripts/new-graph-auth-url.ps1`
  - `scripts/set-graph-refresh-token-secret.ps1`
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

## Required Entra App Setup

Create or update an Azure Entra App Registration:

- Platform redirect URI: `http://localhost`
- Delegated Microsoft Graph permissions:
  - `User.Read`
  - `Chat.ReadWrite`
  - `ChatMessage.Send`
  - `offline_access`
- Grant admin consent if required by tenant policy.

## Local Secret Setup

Use .NET User Secrets for sensitive values. Do not commit real tenant, client, secret, or refresh token values.

Generate an authorization URL:

```powershell
.\scripts\new-graph-auth-url.ps1 -TenantId "<tenant-id>" -ClientId "<client-id>"
```

If script execution is blocked on Windows, run it with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-graph-auth-url.ps1 -TenantId "<tenant-id>" -ClientId "<client-id>"
```

Open the URL, sign in as the sender account, approve permissions, and copy the `code` value from the redirect URL.

Exchange the code and save secrets:

```powershell
.\scripts\set-graph-refresh-token-secret.ps1 `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -ClientSecret "<client-secret>" `
  -AuthorizationCode "<authorization-code>"
```

Set sender and target users:

```powershell
dotnet user-secrets set "Teams:SenderUserEmail" "sender@your-domain.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:TargetUserEmail" "alvint@amulaire.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:SendMode" "Graph" --project .\TL_ORR\TL_ORR.csproj
```

For deployment, use environment variables:

```powershell
$env:MSSQL_CONNECTION_STRING = "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
$env:Teams__SendMode = "Graph"
$env:Teams__AuthMode = "DelegatedRefreshToken"
$env:Teams__TenantId = "<tenant-id>"
$env:Teams__ClientId = "<client-id>"
$env:Teams__ClientSecret = "<client-secret>"
$env:Teams__RefreshToken = "<refresh-token>"
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
