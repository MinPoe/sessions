# Removes the Sessions browser native messaging host registration
#
#     powershell -ExecutionPolicy Bypass -File tools\browser-host\uninstall.ps1

$ErrorActionPreference = 'Stop'
$hostName = 'com.sessions.browser'

$regKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$hostName"
if (Test-Path $regKey) {
    Remove-Item -Path $regKey -Force
    Write-Host "Removed registry key $regKey"
}

$manifestPath = Join-Path $env:APPDATA "Sessions\browser\$hostName.json"
if (Test-Path $manifestPath) {
    Remove-Item -Path $manifestPath -Force
    Write-Host "Removed manifest $manifestPath"
}

Write-Host "Done, remove the unpacked extension from chrome://extensions separately if you loaded it"
