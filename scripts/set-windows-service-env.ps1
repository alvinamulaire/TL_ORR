param(
    [Parameter(Mandatory = $true)]
    [string]$SqlConnectionString,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Graph", "AmulaireMailApi", "Console")]
    [string]$SendMode,

    [string]$TenantId,
    [string]$ClientId,
    [string]$TokenCacheName = "TL-ORR-Teams-Delegated",
    [string]$SenderUserEmail,
    [string]$TargetUserEmail,
    [string]$MailApiUrl,
    [string]$MailApiKey,
    [string]$CcTo,
    [ValidateSet("User", "Machine")]
    [string]$Target = "Machine"
)

$ErrorActionPreference = "Stop"

function Set-RequiredEnvironmentVariable {
    param(
        [string]$Name,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        [Environment]::SetEnvironmentVariable($Name, $Value, $Target)
        Write-Host "Set $Target environment variable: $Name"
    }
}

Set-RequiredEnvironmentVariable -Name "MSSQL_CONNECTION_STRING" -Value $SqlConnectionString
Set-RequiredEnvironmentVariable -Name "Teams__SendMode" -Value $SendMode
Set-RequiredEnvironmentVariable -Name "Teams__AuthMode" -Value "DeviceCode"
Set-RequiredEnvironmentVariable -Name "Teams__TenantId" -Value $TenantId
Set-RequiredEnvironmentVariable -Name "Teams__ClientId" -Value $ClientId
Set-RequiredEnvironmentVariable -Name "Teams__TokenCacheName" -Value $TokenCacheName
Set-RequiredEnvironmentVariable -Name "Teams__SenderUserEmail" -Value $SenderUserEmail
Set-RequiredEnvironmentVariable -Name "Teams__TargetUserEmail" -Value $TargetUserEmail
Set-RequiredEnvironmentVariable -Name "Teams__MailApiUrl" -Value $MailApiUrl
Set-RequiredEnvironmentVariable -Name "Teams__MailApiKey" -Value $MailApiKey
Set-RequiredEnvironmentVariable -Name "Teams__CcTo" -Value $CcTo

Write-Host "Environment variables saved. Restart the Windows Service after changing these values."
