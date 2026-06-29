param(
    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj"
)

dotnet user-secrets set "Teams:SendMode" "Console" --project $ProjectPath

Write-Host "Teams Console mode enabled in .NET User Secrets."
Write-Host "The worker will log notification content without sending real Teams messages."
