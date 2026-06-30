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
- The message includes employee number, SFC, Tool ID, Tool SN, check result, check time, image path, and an image link.
- The image path is converted from `C:\ImageBackup\...` to `\\172.16.2.176\ImageBackup\...`.
- The `dbo.ProductIns` SQL row is updated with `IsSentTeams = 1` and a non-null `SentTeamsTime`.

## Phase 2 Graph Teams Mode

Phase 2 sends the notification through Microsoft Graph. The implementation follows the `AutomateWork` Teams direct message pattern:

- `Microsoft.Graph`
- `Azure.Identity`
- `DeviceCodeCredential`
- persistent token cache

Required delegated Graph permissions:

- `User.Read`
- `User.ReadBasic.All`
- `Chat.Create`
- `Chat.ReadWrite`
- `ChatMessage.Send`
- `offline_access`

Set Graph settings:

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

On first Graph run, the worker logs a device-code sign-in message. Sign in as the sender account. The token is cached under `Teams:TokenCacheName`, so later runs can reuse it.

The Teams message shows both the UNC image path and an `Open image` link. Inline image preview is only added when the image source is an HTTP or HTTPS URL that Teams can access.

Return to Console mode after a real-send test:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\disable-teams-graph-mode.ps1
```

Run a no-send Phase 2 preflight before a real Teams test:

```powershell
$env:TL_ORR_SQL_PASSWORD = "<password>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-phase2-preflight.ps1
```

The preflight checks the .NET project, required Graph user secrets, `sqlcmd`, SQL connectivity, and the `dbo.ProductIns` schema without sending Teams messages.

Environment variable equivalents for deployment:

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

## Phase 2 Graph Acceptance Sample

Run one Graph acceptance cycle. This inserts one pending NG row, enables Graph mode, runs the worker, and restores Console mode when finished:

```powershell
$env:TL_ORR_SQL_PASSWORD = "<password>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-phase2-graph-acceptance.ps1
```

The script generates a unique test SFC, verifies the SQL row after the worker exits, and fails if `IsSentTeams`, `SentTeamsTime`, or `SendErrorMessage` do not match the expected sent state. Use `-KeepGraphMode` only when you want the worker to stay in real Teams sending mode after the test.

During acceptance, the script temporarily sets `Worker:TestSfcFilter` so the Graph run only processes the generated test row.

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

Install with a dedicated service account:

```powershell
$credential = Get-Credential
.\scripts\install-windows-service.ps1 -PublishDirectory .\publish\TL_ORR -Credential $credential
```

Set service environment variables for deployment:

```powershell
.\scripts\set-windows-service-env.ps1 `
  -SqlConnectionString "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;" `
  -SendMode Graph `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -SenderUserEmail "sender@your-domain.com" `
  -TargetUserEmail "alvint@amulaire.com"
```

For Graph DeviceCode mode, complete the first delegated sign-in under the same Windows account that runs the service, so the token cache is available to the service process.

Uninstall:

```powershell
.\scripts\uninstall-windows-service.ps1
```
