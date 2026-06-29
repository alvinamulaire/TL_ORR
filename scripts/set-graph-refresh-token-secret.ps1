param(
    [Parameter(Mandatory = $true)]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [string]$AuthorizationCode,

    [string]$RedirectUri = "http://localhost",

    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj"
)

$tokenUrl = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
$body = @{
    client_id     = $ClientId
    client_secret = $ClientSecret
    code          = $AuthorizationCode
    redirect_uri  = $RedirectUri
    grant_type    = "authorization_code"
    scope         = "offline_access User.Read Chat.ReadWrite ChatMessage.Send"
}

$token = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $body -ContentType "application/x-www-form-urlencoded"

if ([string]::IsNullOrWhiteSpace($token.refresh_token)) {
    throw "Token response did not include refresh_token. Confirm offline_access was granted."
}

dotnet user-secrets set "Teams:TenantId" $TenantId --project $ProjectPath
dotnet user-secrets set "Teams:ClientId" $ClientId --project $ProjectPath
dotnet user-secrets set "Teams:ClientSecret" $ClientSecret --project $ProjectPath
dotnet user-secrets set "Teams:RefreshToken" $token.refresh_token --project $ProjectPath
dotnet user-secrets set "Teams:AuthMode" "DelegatedRefreshToken" --project $ProjectPath

Write-Host "Saved Graph delegated token settings to .NET User Secrets."
