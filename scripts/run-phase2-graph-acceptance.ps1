param(
    [string]$Server = "172.16.2.176",
    [string]$Database = "amulaire_OCR",
    [string]$User = "sa",
    [string]$Password = $env:TL_ORR_SQL_PASSWORD,
    [string]$Sfc = "PHASE2-GRAPH-TEST-$(Get-Date -Format 'yyyyMMddHHmmss')",
    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj",
    [string]$SampleScriptPath = ".\database\004_insert_productins_phase2_graph_sample.sql",
    [switch]$KeepGraphMode
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Password)) {
    $securePassword = Read-Host "SQL password" -AsSecureString
    $Password = [System.Net.NetworkCredential]::new("", $securePassword).Password
}

Write-Host "Inserting Phase 2 Graph acceptance sample row. SFC=$Sfc"
sqlcmd -S $Server -d $Database -U $User -P $Password -C -b -v Sfc="$Sfc" -i $SampleScriptPath

Write-Host "Enabling Teams Graph mode..."
dotnet user-secrets set "Teams:SendMode" "Graph" --project $ProjectPath | Out-Host
dotnet user-secrets set "Worker:TestSfcFilter" "$Sfc" --project $ProjectPath | Out-Host

try {
    Write-Host "Running TL_ORR worker. Follow the device-code sign-in prompt if it appears."
    dotnet run --project $ProjectPath

    Write-Host "Checking Phase 2 Graph acceptance result..."
    $escapedSfc = $Sfc.Replace("'", "''")
    $verificationQuery = @"
SET NOCOUNT ON;
SELECT TOP (1)
    CASE
        WHEN IsSentTeams = 1 AND SentTeamsTime IS NOT NULL AND SendErrorMessage IS NULL THEN 'PASS'
        ELSE 'FAIL'
    END AS AcceptanceResult,
    ID,
    SFC,
    IsSentTeams,
    CONVERT(varchar(19), SentTeamsTime, 120) AS SentTeamsTime,
    ISNULL(SendErrorMessage, '') AS SendErrorMessage
FROM dbo.ProductIns
WHERE SFC = N'$escapedSfc'
ORDER BY ID DESC;
"@

    $verificationResult = sqlcmd -S $Server -d $Database -U $User -P $Password -C -b -h -1 -W -s "|" -Q $verificationQuery
    $resultLine = $verificationResult | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($resultLine)) {
        throw "Phase 2 acceptance failed. No ProductIns row found for SFC=$Sfc."
    }

    Write-Host "Acceptance result: $resultLine"

    if (-not $resultLine.StartsWith("PASS|")) {
        throw "Phase 2 acceptance failed. Expected sent row for SFC=$Sfc."
    }
}
finally {
    dotnet user-secrets remove "Worker:TestSfcFilter" --project $ProjectPath | Out-Host

    if (-not $KeepGraphMode) {
        Write-Host "Restoring Teams Console mode..."
        dotnet user-secrets set "Teams:SendMode" "Console" --project $ProjectPath | Out-Host
    }
}
