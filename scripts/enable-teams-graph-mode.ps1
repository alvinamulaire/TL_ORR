param(
    [string]$ProjectPath = ".\TL_ORR\TL_ORR.csproj"
)

dotnet user-secrets set "Teams:SendMode" "Graph" --project $ProjectPath

Write-Host "Teams Graph mode enabled in .NET User Secrets."
Write-Host "The next worker run will send real Teams messages."
