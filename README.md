# TL_ORR - Teams NG Notification Worker

## Phase 1 Scope

Phase 1 builds the runnable background workflow without sending real Microsoft Teams messages.

- Run as a .NET Worker Service.
- Connect to MS SQL Server.
- Query rows where `CheckResult = 'NG'` and `IsSentTeams = 0`.
- Use `ProductIns.ID` as the update key.
- Convert `ImagePath` from a local path to a UNC file share path.
- Log the Teams message content as a Phase 1 simulation.
- Mark simulated sends as sent by updating `IsSentTeams = 1` and `SentTeamsTime = GETDATE()`.
- Use `Worker:RunOnce = true` in Development for one-cycle acceptance testing.

## SQL Scripts

Run these scripts in order:

- `database/001_productins_phase1_index.sql`: create the pending notification index for `dbo.ProductIns`.
- `database/002_insert_productins_phase1_sample.sql`: insert one NG sample row into `dbo.ProductIns`.
- `database/003_phase1_acceptance_check.sql`: inspect the latest rows after running the worker.

## Configuration

For Phase 1, keep `Teams:SendMode` as `Console`.

Use `TL_ORR/appsettings.Example.json` as the reference. Update these values in `TL_ORR/appsettings.Development.json` for local testing:

- `ConnectionStrings:DefaultConnection`
- `Teams:TargetUserEmail`
- `Worker:IntervalSeconds`
- `Worker:BatchSize`
- `Worker:RunOnce`
- `FileShare:ServerIP`
- `FileShare:ShareName`

Like `TestWebApp`, SQL connection can also be supplied through the `MSSQL_CONNECTION_STRING` environment variable. This is preferred for deployment.

## Run Phase 1

```powershell
dotnet run --project .\TL_ORR\TL_ORR.csproj
```

Development defaults to `Worker:RunOnce = true`, so the worker runs one cycle and stops.

## Phase 1 Acceptance

- The log contains `Phase 1 Teams message simulation`.
- The message includes employee number, SFC, Tool ID, Tool SN, check result, image path, and check time.
- The image path is converted from `C:\ImageBackup\...` to `\\192.168.1.100\ImageBackup\...`.
- The `dbo.ProductIns` SQL row is updated with `IsSentTeams = 1` and a non-null `SentTeamsTime`.

## Phase 2 Graph Mode

Phase 2 sends the notification through Microsoft Graph.

Keep real secrets in .NET User Secrets, not in `appsettings.json`.

Required delegated Graph permissions:

- `User.Read`
- `Chat.ReadWrite`
- `ChatMessage.Send`
- `offline_access`

Create the authorization URL:

```powershell
.\scripts\new-graph-auth-url.ps1 -TenantId "<tenant-id>" -ClientId "<client-id>"
```

If script execution is blocked on Windows, run it with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-graph-auth-url.ps1 -TenantId "<tenant-id>" -ClientId "<client-id>"
```

Open the URL, sign in as the Teams sender account, and copy the returned `code`.

Exchange the code for a refresh token and save Graph secrets:

```powershell
.\scripts\set-graph-refresh-token-secret.ps1 `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -ClientSecret "<client-secret>" `
  -AuthorizationCode "<authorization-code>"
```

Set sender/target and enable Graph mode:

```powershell
dotnet user-secrets set "Teams:SenderUserEmail" "sender@your-domain.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:TargetUserEmail" "alvint@amulaire.com" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:SendMode" "Graph" --project .\TL_ORR\TL_ORR.csproj
```

Environment variable equivalents for deployment:

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

## Phase 2 Amulaire Mail API Mode

`AmulaireService` uses an internal mail API:

- `SendMailApiUrl`
- `X-Api-Key`
- `SendMailDto`

TL_ORR can use the same integration through `Teams:SendMode = AmulaireMailApi`.

Store the API URL and key in User Secrets:

```powershell
dotnet user-secrets set "Teams:MailApiUrl" "<mail-api-url>" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:MailApiKey" "<mail-api-key>" --project .\TL_ORR\TL_ORR.csproj
dotnet user-secrets set "Teams:SendMode" "AmulaireMailApi" --project .\TL_ORR\TL_ORR.csproj
```

The API request body follows the AmulaireService `SendMailDto` shape:

- `MailTo`
- `CcTo`
- `MailSubjict`
- `MailBody`
- `IsBodyHtmlFormat`
- `UseTemplate`

## Publish

```powershell
dotnet publish .\TL_ORR\TL_ORR.csproj -c Release -o .\publish\TL_ORR
```

## Windows Service

Install after publishing:

```powershell
.\scripts\install-windows-service.ps1 -PublishDirectory .\publish\TL_ORR
Start-Service "TL_ORR Teams NG Notify Service"
```

Uninstall:

```powershell
.\scripts\uninstall-windows-service.ps1
```
