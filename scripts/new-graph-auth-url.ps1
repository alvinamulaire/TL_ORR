param(
    [Parameter(Mandatory = $true)]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [string]$RedirectUri = "http://localhost",

    [string]$State = "tl-orr-phase2"
)

$scopes = @(
    "offline_access",
    "User.Read",
    "Chat.ReadWrite",
    "ChatMessage.Send"
) -join " "

$queryParts = @(
    "client_id=$([uri]::EscapeDataString($ClientId))",
    "response_type=code",
    "redirect_uri=$([uri]::EscapeDataString($RedirectUri))",
    "response_mode=query",
    "scope=$([uri]::EscapeDataString($scopes))",
    "state=$([uri]::EscapeDataString($State))"
)

$url = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/authorize?$($queryParts -join '&')"
Write-Host $url
