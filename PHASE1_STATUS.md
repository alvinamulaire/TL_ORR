# Phase 1 Status

## Completed

- .NET 8 Worker Service project is implemented.
- SQL Server access layer is implemented with `Microsoft.Data.SqlClient`.
- Pending NG query is implemented:
  - `CheckResult = 'NG'`
  - `IsSentTeams = 0`
  - ordered by `DateTime ASC`
  - limited by `Worker:BatchSize`
- Phase 1 Teams simulation is implemented with `Teams:SendMode = Console`.
- Notification message includes:
  - employee number
  - SFC
  - Tool ID
  - Tool SN
  - check result
  - image path
  - check time
- Local image path to UNC path conversion is implemented.
- Success status update is implemented:
  - `IsSentTeams = 1`
  - `SentTeamsTime = GETDATE()`
  - `SendErrorMessage = NULL`
- Failure status update is implemented:
  - `SendErrorMessage = @ErrorMessage`
- Single-row failure handling does not stop the remaining batch.
- `Worker:RunOnce` is implemented for one-cycle Phase 1 testing.
- SQL scripts are provided under `database/`.
- Windows Service publish/install/uninstall scripts are provided under `scripts/`.
- Startup configuration validation is implemented.

## Verified Locally

- `dotnet build .\TL_ORR.slnx`
- `dotnet publish .\TL_ORR\TL_ORR.csproj -c Release -o .\publish\TL_ORR`

Both commands completed successfully with 0 warnings and 0 errors.

## Requires Site Configuration

These items require a real SQL Server/database connection before final Phase 1 acceptance:

- Update `TL_ORR/appsettings.Development.json`:
  - `ConnectionStrings:DefaultConnection`
  - `FileShare:ServerIP`
  - `FileShare:ShareName`
  - `Teams:TargetUserEmail`
- Run `database/001_create_tool_check_result.sql`.
- Run `database/002_insert_phase1_sample.sql`.
- Run the worker:

```powershell
dotnet run --project .\TL_ORR\TL_ORR.csproj
```

- Confirm the log contains `Phase 1 Teams message simulation`.
- Run `database/003_phase1_acceptance_check.sql`.
- Confirm the sample row has `IsSentTeams = 1` and a non-null `SentTeamsTime`.

## Phase 2 Entry Criteria

Phase 2 can begin after the site SQL acceptance check passes. Phase 2 should switch `Teams:SendMode` from `Console` to `Graph` and validate Microsoft Graph app registration, permissions, token retrieval, chat creation, and real Teams message delivery.
