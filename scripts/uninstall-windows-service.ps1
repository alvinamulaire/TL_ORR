param(
    [string]$ServiceName = "TL_ORR Teams NG Notify Service"
)

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Host "Windows Service not found: $ServiceName"
    return
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force
}

sc.exe delete $ServiceName | Out-Host
Write-Host "Deleted Windows Service: $ServiceName"
