param(
    [string]$ServiceName = "TL_ORR Teams NG Notify Service",
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    throw "Windows Service not found: $ServiceName"
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName
    $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds($TimeoutSeconds))
}

Start-Service -Name $ServiceName
$service.WaitForStatus("Running", [TimeSpan]::FromSeconds($TimeoutSeconds))

Get-Service -Name $ServiceName | Select-Object Name, DisplayName, Status, StartType | Format-List
