# Phase 3 Status

## Goal

Phase 3 makes TL_ORR ready for production-style Windows Service deployment and operations after Phase 2 Teams delivery has been validated.

## Current Status

Phase 3 implementation is complete. The service can be published, installed, configured with environment variables, checked before deployment, restarted, and inspected after installation.

## Implemented

- Windows Event Log provider is enabled with source name `TL_ORR Teams NG Notify Service`.
- Worker startup logs include send mode, target user, interval, batch size, failure threshold, run-once mode, and file share path.
- `Worker:StopAfterConsecutiveCycleFailures` stops the host after a configured number of consecutive cycle-level failures.
- Development keeps `StopAfterConsecutiveCycleFailures = 0`, so local testing remains forgiving.
- Production default uses `StopAfterConsecutiveCycleFailures = 5`.
- Windows Service installation supports a dedicated service account.
- Windows Service environment variable setup includes SQL, Teams, and Worker settings.
- Phase 3 deployment preflight validates publish output, required environment variables, optional service installation, and Event Log readability.
- Service status and restart scripts are available.

## Scripts

- `scripts/set-windows-service-env.ps1`: save deployment environment variables.
- `scripts/install-windows-service.ps1`: install the published worker as a Windows Service.
- `scripts/get-windows-service-status.ps1`: inspect service status and service account/path.
- `scripts/restart-windows-service.ps1`: restart the Windows Service and wait for running status.
- `scripts/test-phase3-deployment-preflight.ps1`: run no-send deployment checks.
- `scripts/uninstall-windows-service.ps1`: remove the Windows Service.

## Acceptance

1. Publish the worker:

```powershell
dotnet publish .\TL_ORR\TL_ORR.csproj -c Release -o .\publish\TL_ORR
```

2. Configure environment variables:

```powershell
.\scripts\set-windows-service-env.ps1 `
  -SqlConnectionString "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;" `
  -SendMode Graph `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -SenderUserEmail "sender@your-domain.com" `
  -TargetUserEmail "alvint@amulaire.com"
```

3. Run deployment preflight:

```powershell
.\scripts\test-phase3-deployment-preflight.ps1
```

4. Install and start the service:

```powershell
$credential = Get-Credential
.\scripts\install-windows-service.ps1 -PublishDirectory .\publish\TL_ORR -Credential $credential
Start-Service "TL_ORR Teams NG Notify Service"
```

5. Inspect service status:

```powershell
.\scripts\get-windows-service-status.ps1
```

6. Confirm logs in Windows Event Viewer under Application log source `TL_ORR Teams NG Notify Service`.

## Operational Notes

- For Graph DeviceCode mode, complete the first delegated sign-in under the same Windows account that runs the service.
- Keep `Worker__RunOnce=false` for Windows Service deployment.
- Keep `Worker__StopAfterConsecutiveCycleFailures` greater than `0` in production so repeated cycle failures stop the service and surface to service monitoring.
- Use Console mode for dry-run service checks where real Teams delivery is not desired.
