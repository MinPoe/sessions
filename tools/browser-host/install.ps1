# Installs the Sessions browser native messaging host for Chrome
#
# Writes the host manifest and the HKCU registry key Chrome reads to find it, the
# allowed_origins is pinned to the extension id computed from the extension's
# manifest key, so it stays correct wherever the unpacked extension is loaded from,
# see docs/spikes/browser.md
#
#     powershell -ExecutionPolicy Bypass -File tools\browser-host\install.ps1
#
# Pass -HostExePath to point at a Release build or a published exe

param(
    [string]$HostExePath,
    [string]$ExtensionManifest
)

$ErrorActionPreference = 'Stop'
$hostName = 'com.sessions.browser'

$scriptDir = Split-Path -Parent $PSCommandPath
$repo = (Resolve-Path (Join-Path $scriptDir '..\..')).Path

if (-not $HostExePath) {
    $HostExePath = Join-Path $repo 'src\Sessions.BrowserHost\bin\Debug\net10.0-windows\BrowserHost.exe'
}
if (-not (Test-Path $HostExePath)) {
    throw "BrowserHost.exe not found at $HostExePath, build it first with 'dotnet build' or pass -HostExePath"
}
$HostExePath = (Resolve-Path $HostExePath).Path

if (-not $ExtensionManifest) {
    $ExtensionManifest = Join-Path $repo 'extension\manifest.json'
}
$extension = Get-Content $ExtensionManifest -Raw | ConvertFrom-Json
if (-not $extension.key) {
    throw "extension manifest has no 'key', cannot pin the extension id"
}

# Extension id, first 16 bytes of SHA-256 of the DER public key, each nibble a-p
$der = [Convert]::FromBase64String($extension.key)
$sha = [System.Security.Cryptography.SHA256]::Create().ComputeHash($der)
$extensionId = -join (0..15 | ForEach-Object {
    [char](97 + ($sha[$_] -shr 4)) + [char](97 + ($sha[$_] -band 15))
})

# Host manifest, allowed_origins is the exact extension id with a trailing slash
$manifestDir = Join-Path $env:APPDATA 'Sessions\browser'
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
$manifestPath = Join-Path $manifestDir "$hostName.json"

$manifest = [ordered]@{
    name            = $hostName
    description     = 'Sessions browser relay'
    path            = $HostExePath
    type            = 'stdio'
    allowed_origins = @("chrome-extension://$extensionId/")
}
# Write without a BOM, Chrome's manifest parser is happier with plain UTF-8
$json = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding $false))

# Registry key Chrome reads, default value is the manifest path
$regKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$hostName"
New-Item -Path $regKey -Force | Out-Null
Set-ItemProperty -Path $regKey -Name '(default)' -Value $manifestPath

Write-Host "Installed native messaging host '$hostName'"
Write-Host "  host exe     : $HostExePath"
Write-Host "  manifest     : $manifestPath"
Write-Host "  registry     : $regKey"
Write-Host "  extension id : $extensionId"
Write-Host ''
Write-Host "Next, load the 'extension' folder as an unpacked extension at chrome://extensions,"
Write-Host "then confirm its id shown there matches the one above"
