param(
    [Parameter(Mandatory = $true)]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$SenderUserEmail,

    [Parameter(Mandatory = $true)]
    [string]$TargetUserEmail,

    [string]$TokenCacheName = "TL-ORR-Teams-Delegated",

    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj"
)

dotnet user-secrets set "Teams:TenantId" $TenantId --project $ProjectPath
dotnet user-secrets set "Teams:ClientId" $ClientId --project $ProjectPath
dotnet user-secrets set "Teams:SenderUserEmail" $SenderUserEmail --project $ProjectPath
dotnet user-secrets set "Teams:TargetUserEmail" $TargetUserEmail --project $ProjectPath
dotnet user-secrets set "Teams:TokenCacheName" $TokenCacheName --project $ProjectPath
dotnet user-secrets set "Teams:AuthMode" "DeviceCode" --project $ProjectPath

Write-Host "Saved Teams Graph DeviceCode settings to .NET User Secrets."
Write-Host "To enable real Teams sending, run:"
Write-Host "dotnet user-secrets set `"Teams:SendMode`" `"Graph`" --project $ProjectPath"
