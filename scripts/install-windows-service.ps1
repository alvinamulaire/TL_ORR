param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [string]$ServiceName = "TL_ORR Teams NG Notify Service",

    [System.Management.Automation.PSCredential]$Credential
)

$exePath = Join-Path -Path $PublishDirectory -ChildPath "TL_ORR.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Service executable not found: $exePath"
}

$serviceParameters = @{
    Name = $ServiceName
    BinaryPathName = "`"$exePath`""
    DisplayName = $ServiceName
    StartupType = "Automatic"
}

if ($null -ne $Credential) {
    $serviceParameters.Credential = $Credential
}

New-Service @serviceParameters

Write-Host "Installed Windows Service: $ServiceName"
if ($null -ne $Credential) {
    Write-Host "Service account: $($Credential.UserName)"
}
