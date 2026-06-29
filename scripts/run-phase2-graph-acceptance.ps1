param(
    [string]$Server = "172.16.2.176",
    [string]$Database = "amulaire_OCR",
    [string]$User = "sa",
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj",
    [string]$SampleScriptPath = ".\database\004_insert_productins_phase2_graph_sample.sql",
    [switch]$KeepGraphMode
)

$ErrorActionPreference = "Stop"

Write-Host "Inserting Phase 2 Graph acceptance sample row..."
sqlcmd -S $Server -d $Database -U $User -P $Password -C -i $SampleScriptPath

Write-Host "Enabling Teams Graph mode..."
dotnet user-secrets set "Teams:SendMode" "Graph" --project $ProjectPath | Out-Host

try {
    Write-Host "Running TL_ORR worker. Follow the device-code sign-in prompt if it appears."
    dotnet run --project $ProjectPath
}
finally {
    if (-not $KeepGraphMode) {
        Write-Host "Restoring Teams Console mode..."
        dotnet user-secrets set "Teams:SendMode" "Console" --project $ProjectPath | Out-Host
    }
}
