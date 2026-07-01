# TL_ORR Server Deployment Guide

## Package Contents

- `app/`: published TL_ORR Worker executable and runtime files.
- `scripts/`: deployment, service, preflight, and health-check scripts.
- `database/`: SQL helper scripts.
- `docs/`: project documents and phase status files.

## Deployment Flow

1. Copy the deployment package to the Windows Server.
2. Extract the zip to a stable directory, for example:

```powershell
C:\Services\TL_ORR
```

3. Configure environment variables. Run PowerShell as Administrator:

```powershell
.\scripts\set-windows-service-env.ps1 `
  -SqlConnectionString "Server=172.16.2.176;Database=amulaire_OCR;User Id=<user>;Password=<password>;TrustServerCertificate=True;" `
  -NotificationRecipientsConnectionString "Server=192.168.3.35;Database=AlertDB;User Id=<user>;Password=<password>;TrustServerCertificate=True;" `
  -SendMode Graph `
  -TenantId "<tenant-id>" `
  -ClientId "<client-id>" `
  -SenderUserEmail "mesalm@amulaire.com" `
  -TargetUserEmail "alvint@amulaire.com"
```

4. Run deployment preflight:

```powershell
.\scripts\test-phase3-deployment-preflight.ps1 -PublishDirectory .\app
```

5. Install the Windows Service:

```powershell
$credential = Get-Credential
.\scripts\install-windows-service.ps1 -PublishDirectory .\app -Credential $credential
```

6. Complete Graph DeviceCode sign-in once using the same Windows account that runs the service.

7. Start the service:

```powershell
Start-Service "TL_ORR Teams NG Notify Service"
```

8. Check service status and SQL health:

```powershell
.\scripts\get-windows-service-status.ps1
$env:TL_ORR_SQL_PASSWORD = "<password>"
.\scripts\test-system-health.ps1
Remove-Item Env:\TL_ORR_SQL_PASSWORD
```

## Important Notes

- Do not commit or store SQL passwords, Graph secrets, or API keys in the package.
- `Teams__SendMode=Console` logs notifications without sending Teams messages.
- `Teams__SendMode=Graph` sends real Teams messages.
- Production polling interval is controlled by `Worker__IntervalSeconds`.
- Production retry protection is controlled by `Worker__StopAfterConsecutiveCycleFailures`.
- The Teams message displays the UNC image path as text:

```text
\\172.16.2.176\ImageBackup\...
```

## Uninstall

```powershell
.\scripts\uninstall-windows-service.ps1
```
