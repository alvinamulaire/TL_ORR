param(
    [string]$Server = "172.16.2.176",
    [string]$Database = "amulaire_OCR",
    [string]$User = "sa",
    [string]$Password = $env:TL_ORR_SQL_PASSWORD,
    [string]$ServiceName = "TL_ORR Teams NG Notify Service",
    [switch]$SkipSql
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

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Add-Warn "Windows Service not installed: $ServiceName"
}
elseif ($service.Status -eq "Running") {
    Add-Pass "Windows Service is running: $ServiceName"
}
else {
    Add-Warn "Windows Service is installed but not running: $ServiceName ($($service.Status))"
}

if (-not $SkipSql) {
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Add-Failure "sqlcmd is not available."
    }
    else {
        Add-Pass "sqlcmd is available."
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        $securePassword = Read-Host "SQL password" -AsSecureString
        $Password = [System.Net.NetworkCredential]::new("", $securePassword).Password
    }

    $healthQuery = @"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.ProductIns', N'U') IS NULL
BEGIN
    SELECT 'MISSING_TABLE|0|0|0';
    RETURN;
END;

SELECT CONCAT(
    'OK_TABLE|',
    COUNT(CASE WHEN CheckResult = 'NG' AND IsSentTeams = 0 THEN 1 END),
    '|',
    COUNT(CASE WHEN CheckResult = 'NG' AND IsSentTeams = 0 AND NULLIF(SendErrorMessage, '') IS NOT NULL THEN 1 END),
    '|',
    COUNT(CASE WHEN CheckResult = 'NG' AND IsSentTeams = 1 THEN 1 END)
)
FROM dbo.ProductIns;
"@

    $sqlResult = sqlcmd -S $Server -d $Database -U $User -P $Password -C -b -h -1 -W -s "|" -Q $healthQuery
    $resultLine = $sqlResult | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($resultLine)) {
        Add-Failure "SQL health query returned no result."
    }
    elseif ($resultLine.StartsWith("OK_TABLE|")) {
        $parts = $resultLine.Split("|")
        Add-Pass "SQL ProductIns table is available."
        Write-Host "Pending NG count: $($parts[1])"
        Write-Host "Failed pending count: $($parts[2])"
        Write-Host "Sent NG count: $($parts[3])"

        if ([int]$parts[2] -gt 0) {
            Add-Warn "There are pending rows with SendErrorMessage. Review Teams/SQL errors before relying on automatic retry."
        }
    }
    else {
        Add-Failure "SQL health check failed. $resultLine"
    }
}

if ($failedChecks.Count -gt 0) {
    throw "System health check failed with $($failedChecks.Count) issue(s)."
}

Write-Host "System health check completed."
