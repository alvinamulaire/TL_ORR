# TL_ORR - Teams NG Notification Worker

## Phase 1 Scope

Phase 1 builds the runnable background workflow without sending real Microsoft Teams messages.

- Run as a .NET Worker Service.
- Connect to MS SQL Server.
- Query rows where `CheckResult = 'NG'` and `IsSentTeams = 0`.
- Convert `ImagePath` from a local path to a UNC file share path.
- Log the Teams message content as a Phase 1 simulation.
- Mark simulated sends as sent by updating `IsSentTeams = 1` and `SentTeamsTime = GETDATE()`.
- Use `Worker:RunOnce = true` in Development for one-cycle acceptance testing.

## SQL Scripts

Run these scripts in order:

- `database/001_create_tool_check_result.sql`: create `dbo.ToolCheckResult` and the pending notification index.
- `database/002_insert_phase1_sample.sql`: insert one NG sample row.
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

## Run Phase 1

```powershell
dotnet run --project .\TL_ORR\TL_ORR.csproj
```

Development defaults to `Worker:RunOnce = true`, so the worker runs one cycle and stops.

## Phase 1 Acceptance

- The log contains `Phase 1 Teams message simulation`.
- The message includes employee number, SFC, Tool ID, Tool SN, check result, image path, and check time.
- The image path is converted from `C:\ImageBackup\...` to `\\192.168.1.100\ImageBackup\...`.
- The SQL row is updated with `IsSentTeams = 1` and a non-null `SentTeamsTime`.

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
