param(
    [Parameter(Mandatory = $true)]
    [string]$SqlConnectionString,

    [Parameter(Mandatory = $true)]
    [string]$NotificationRecipientsConnectionString,

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
    [int]$IntervalSeconds = 60,
    [int]$BatchSize = 100,
    [int]$StopAfterConsecutiveCycleFailures = 5,
    [int]$SqlCommandTimeoutSeconds = 30,
    [int]$PerRecordTimeoutSeconds = 120,
    [int]$TeamsHttpTimeoutSeconds = 120,
    [bool]$TeamsInlineImageEnabled = $true,
    [int]$TeamsMaxInlineImageBytes = 4194304,
    [int]$NotificationRecipientsProjectGroup = 1,
    [int]$NotificationRecipientsCommandTimeoutSeconds = 30,
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
Set-RequiredEnvironmentVariable -Name "NOTIFICATION_RECIPIENTS_CONNECTION_STRING" -Value $NotificationRecipientsConnectionString
Set-RequiredEnvironmentVariable -Name "NotificationRecipients__Source" -Value "SqlServer"
Set-RequiredEnvironmentVariable -Name "NotificationRecipients__ProjectGroup" -Value $NotificationRecipientsProjectGroup.ToString()
Set-RequiredEnvironmentVariable -Name "NotificationRecipients__CommandTimeoutSeconds" -Value $NotificationRecipientsCommandTimeoutSeconds.ToString()
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
Set-RequiredEnvironmentVariable -Name "Teams__HttpTimeoutSeconds" -Value $TeamsHttpTimeoutSeconds.ToString()
Set-RequiredEnvironmentVariable -Name "Teams__InlineImageEnabled" -Value $TeamsInlineImageEnabled.ToString().ToLowerInvariant()
Set-RequiredEnvironmentVariable -Name "Teams__MaxInlineImageBytes" -Value $TeamsMaxInlineImageBytes.ToString()
Set-RequiredEnvironmentVariable -Name "Worker__IntervalSeconds" -Value $IntervalSeconds.ToString()
Set-RequiredEnvironmentVariable -Name "Worker__BatchSize" -Value $BatchSize.ToString()
Set-RequiredEnvironmentVariable -Name "Worker__RunOnce" -Value "false"
Set-RequiredEnvironmentVariable -Name "Worker__StopAfterConsecutiveCycleFailures" -Value $StopAfterConsecutiveCycleFailures.ToString()
Set-RequiredEnvironmentVariable -Name "Worker__SqlCommandTimeoutSeconds" -Value $SqlCommandTimeoutSeconds.ToString()
Set-RequiredEnvironmentVariable -Name "Worker__PerRecordTimeoutSeconds" -Value $PerRecordTimeoutSeconds.ToString()

Write-Host "Environment variables saved. Restart the Windows Service after changing these values."
