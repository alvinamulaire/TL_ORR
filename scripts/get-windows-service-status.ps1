param(
    [string]$ServiceName = "TL_ORR Teams NG Notify Service"
)

$ErrorActionPreference = "Stop"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Host "Windows Service not found: $ServiceName"
    exit 1
}

$service | Select-Object Name, DisplayName, Status, StartType | Format-List

$process = Get-CimInstance Win32_Service -Filter "Name = '$ServiceName'" -ErrorAction SilentlyContinue
if ($null -ne $process) {
    $process | Select-Object Name, State, StartMode, StartName, PathName | Format-List
}
