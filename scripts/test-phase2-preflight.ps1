param(
    [string]$Server = "172.16.2.176",
    [string]$Database = "amulaire_OCR",
    [string]$User = "sa",
    [string]$Password = $env:TL_ORR_SQL_PASSWORD,
    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj",
    [switch]$SkipBuild,
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

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Add-Pass "dotnet CLI is available."
}
else {
    Add-Failure "dotnet CLI is not available."
}

if (-not $SkipSql) {
    if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
        Add-Pass "sqlcmd is available."
    }
    else {
        Add-Failure "sqlcmd is not available."
    }
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Add-Failure "Project file not found: $ProjectPath"
}
else {
    Add-Pass "Project file found."
}

if (-not $SkipBuild -and $failedChecks.Count -eq 0) {
    dotnet build $ProjectPath | Out-Host
    Add-Pass "Project build completed."
}

$secretOutput = dotnet user-secrets list --project $ProjectPath 2>$null
$secretKeys = @{}
foreach ($line in $secretOutput) {
    $separatorIndex = $line.IndexOf(" = ")
    if ($separatorIndex -gt 0) {
        $secretKeys[$line.Substring(0, $separatorIndex)] = $true
    }
}

$requiredSecrets = @(
    "Teams:TenantId",
    "Teams:ClientId",
    "Teams:SenderUserEmail",
    "NotificationRecipients:ConnectionString"
)

foreach ($secretName in $requiredSecrets) {
    if ($secretKeys.ContainsKey($secretName)) {
        Add-Pass "User secret exists: $secretName"
    }
    else {
        Add-Failure "Missing user secret: $secretName"
    }
}

$recipientConnectionStringLine = $secretOutput | Where-Object { $_.StartsWith("NotificationRecipients:ConnectionString = ", [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
if ($recipientConnectionStringLine -and -not $SkipSql) {
    $recipientConnectionString = $recipientConnectionStringLine.Substring("NotificationRecipients:ConnectionString = ".Length)
    $recipientQuery = @"
SET NOCOUNT ON;
SELECT COUNT(*)
FROM dbo.N8N_NotifyLevel_ATT
WHERE Project_Group = 1
  AND NULLIF(LTRIM(RTRIM(CAST(Email AS nvarchar(320)))), '') IS NOT NULL;
"@

    $recipientConnection = New-Object System.Data.SqlClient.SqlConnection($recipientConnectionString)
    try {
        $recipientConnection.Open()
        $recipientCommand = $recipientConnection.CreateCommand()
        $recipientCommand.CommandText = $recipientQuery
        $recipientCommand.CommandTimeout = 30
        $recipientCount = [int]$recipientCommand.ExecuteScalar()

        if ($recipientCount -gt 0) {
            Add-Pass "AlertDB recipient query returned $recipientCount recipient(s)."
        }
        else {
            Add-Failure "AlertDB recipient query returned no recipients for Project_Group=1."
        }
    }
    finally {
        $recipientConnection.Dispose()
    }
}

if ($secretKeys.ContainsKey("Teams:SendMode")) {
    Add-Pass "User secret exists: Teams:SendMode"
}
else {
    Add-Warn "Teams:SendMode user secret is not set. appsettings default may be used."
}

if (-not $SkipSql) {
    if ([string]::IsNullOrWhiteSpace($Password)) {
        $securePassword = Read-Host "SQL password" -AsSecureString
        $Password = [System.Net.NetworkCredential]::new("", $securePassword).Password
    }

    $schemaCheckQuery = @"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.ProductIns', N'U') IS NULL
BEGIN
    SELECT 'MISSING_TABLE|MISSING_ID|MISSING_IsSentTeams|0';
    RETURN;
END;

DECLARE @PendingCount int = 0;
IF COL_LENGTH(N'dbo.ProductIns', N'CheckResult') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProductIns', N'IsSentTeams') IS NOT NULL
BEGIN
    SELECT @PendingCount = COUNT(*)
    FROM dbo.ProductIns
    WHERE CheckResult = 'NG'
      AND IsSentTeams = 0;
END;

SELECT
    CONCAT(
        'OK_TABLE|',
        CASE WHEN COL_LENGTH(N'dbo.ProductIns', N'ID') IS NULL THEN 'MISSING_ID' ELSE 'OK_ID' END,
        '|',
        CASE WHEN COL_LENGTH(N'dbo.ProductIns', N'IsSentTeams') IS NULL THEN 'MISSING_IsSentTeams' ELSE 'OK_IsSentTeams' END,
        '|',
        @PendingCount
    );
"@

    $sqlResult = sqlcmd -S $Server -d $Database -U $User -P $Password -C -b -h -1 -W -s "|" -Q $schemaCheckQuery
    $resultLine = $sqlResult | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($resultLine)) {
        Add-Failure "SQL preflight returned no result."
    }
    elseif ($resultLine.StartsWith("OK_TABLE|OK_ID|OK_IsSentTeams|")) {
        Add-Pass "SQL ProductIns schema is ready. $resultLine"
    }
    else {
        Add-Failure "SQL ProductIns schema check failed. $resultLine"
    }
}

if ($failedChecks.Count -gt 0) {
    throw "Phase 2 preflight failed with $($failedChecks.Count) issue(s)."
}

Write-Host "Phase 2 preflight completed successfully."
