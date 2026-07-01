param(
    [string]$PublishDirectory = ".\publish\TL_ORR",
    [string]$ServiceName = "TL_ORR Teams NG Notify Service",
    [ValidateSet("User", "Machine", "Process")]
    [string]$EnvironmentTarget = "Machine",
    [switch]$RequireInstalledService
)

$ErrorActionPreference = "Stop"
$failedChecks = New-Object System.Collections.Generic.List[string]

function Add-Failure {
    param([string]$Message)
    $failedChecks.Add($Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Add-Pass {
    param([string]$Message)
    Write-Host "[ OK ] $Message" -ForegroundColor Green
}

function Add-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

$exePath = Join-Path -Path $PublishDirectory -ChildPath "TL_ORR.exe"
if (Test-Path -LiteralPath $exePath) {
    Add-Pass "Published executable exists: $exePath"
}
else {
    Add-Failure "Published executable not found: $exePath"
}

$requiredEnvNames = @(
    "MSSQL_CONNECTION_STRING",
    "NOTIFICATION_RECIPIENTS_CONNECTION_STRING",
    "Teams__SendMode",
    "NotificationRecipients__Source"
)

$graphEnvNames = @(
    "Teams__TenantId",
    "Teams__ClientId",
    "Teams__SenderUserEmail"
)

$sendMode = [Environment]::GetEnvironmentVariable("Teams__SendMode", $EnvironmentTarget)

foreach ($name in $requiredEnvNames) {
    $value = [Environment]::GetEnvironmentVariable($name, $EnvironmentTarget)
    if ([string]::IsNullOrWhiteSpace($value)) {
        Add-Failure "Missing $EnvironmentTarget environment variable: $name"
    }
    else {
        Add-Pass "$EnvironmentTarget environment variable exists: $name"
    }
}

if ([string]::Equals($sendMode, "Graph", [StringComparison]::OrdinalIgnoreCase)) {
    foreach ($name in $graphEnvNames) {
        $value = [Environment]::GetEnvironmentVariable($name, $EnvironmentTarget)
        if ([string]::IsNullOrWhiteSpace($value)) {
            Add-Failure "Missing Graph $EnvironmentTarget environment variable: $name"
        }
        else {
            Add-Pass "Graph $EnvironmentTarget environment variable exists: $name"
        }
    }
}
else {
    Add-Warn "Teams__SendMode is '$sendMode'. Graph-specific environment variables were not required."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    if ($RequireInstalledService) {
        Add-Failure "Windows Service not installed: $ServiceName"
    }
    else {
        Add-Warn "Windows Service not installed yet: $ServiceName"
    }
}
else {
    Add-Pass "Windows Service installed: $ServiceName ($($service.Status))"
}

try {
    Get-EventLog -LogName Application -Source $ServiceName -Newest 1 -ErrorAction Stop | Out-Null
    Add-Pass "Windows Event Log source is readable: $ServiceName"
}
catch {
    Add-Warn "Windows Event Log source has no readable entries yet: $ServiceName"
}

if ($failedChecks.Count -gt 0) {
    throw "Phase 3 deployment preflight failed with $($failedChecks.Count) issue(s)."
}

Write-Host "Phase 3 deployment preflight completed successfully."
