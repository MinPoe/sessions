# Sessions terminal hook
#
# Records this shell so the Sessions app can capture it, dot-source it from your
# PowerShell profile ($PROFILE):
#
#     . "C:\path\to\sessions-hook.ps1"
#
# It writes {pid, cwd, shellPath, wtSession, updated} to
# %APPDATA%\Sessions\terminals\<pid>.json, refreshes it on every prompt so the
# cwd stays current, and removes it when the shell exits

$script:SessionsHookDir = Join-Path $env:APPDATA 'Sessions\terminals'
$script:SessionsHookFile = Join-Path $script:SessionsHookDir "$PID.json"
$script:SessionsHookShellPath = (Get-Process -Id $PID).Path

function Write-SessionsTerminalRecord {
    try {
        if (-not (Test-Path $script:SessionsHookDir)) {
            New-Item -ItemType Directory -Path $script:SessionsHookDir -Force | Out-Null
        }

        $record = [ordered]@{
            pid        = $PID
            cwd        = $PWD.ProviderPath
            shellPath  = $script:SessionsHookShellPath
            wtSession  = $env:WT_SESSION
            updated    = (Get-Date).ToString('o')
        }

        # Temp file plus move so a reader never sees a half-written record
        $temp = "$script:SessionsHookFile.tmp"
        $record | ConvertTo-Json -Compress | Set-Content -Path $temp -Encoding utf8
        Move-Item -Path $temp -Destination $script:SessionsHookFile -Force
    }
    catch {
        # A hook must never break the prompt, swallow and carry on
    }
}

function Remove-SessionsTerminalRecord {
    try {
        if (Test-Path $script:SessionsHookFile) {
            Remove-Item -Path $script:SessionsHookFile -Force
        }
    }
    catch { }
}

# Refresh on every prompt so a cd is picked up, wrap any existing prompt rather
# than replace it
if (-not (Test-Path 'function:global:SessionsOriginalPrompt')) {
    $existing = (Get-Command prompt -ErrorAction SilentlyContinue).ScriptBlock
    if ($null -eq $existing) {
        $existing = { "PS $($executionContext.SessionState.Path.CurrentLocation)$('>' * ($nestedPromptLevel + 1)) " }
    }
    Set-Item -Path 'function:global:SessionsOriginalPrompt' -Value $existing
}

function global:prompt {
    Write-SessionsTerminalRecord
    SessionsOriginalPrompt
}

# Remove the record when the shell closes
Register-EngineEvent -SourceIdentifier ([System.Management.Automation.PsEngineEvent]::Exiting) -Action {
    Remove-SessionsTerminalRecord
} | Out-Null

# Write once now so the shell is recorded before the first prompt
Write-SessionsTerminalRecord
