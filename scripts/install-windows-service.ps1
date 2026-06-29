param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [string]$ServiceName = "TL_ORR Teams NG Notify Service"
)

$exePath = Join-Path -Path $PublishDirectory -ChildPath "TL_ORR.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Service executable not found: $exePath"
}

New-Service `
    -Name $ServiceName `
    -BinaryPathName "`"$exePath`"" `
    -DisplayName $ServiceName `
    -StartupType Automatic

Write-Host "Installed Windows Service: $ServiceName"
