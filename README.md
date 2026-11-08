# Sessions

Sessions captures the windows you have open on Windows into a named layout, then reopens them where
they were, it knows how to relaunch an app, place its window, and for a few apps restore what was
inside it,

- VS Code reopens the folder or workspace that was open, not a blank window
- Windows Terminal reopens its tabs in their folders, resumes a Claude Code session, and re-runs an ssh command
- everything else is relaunched by its executable and placed at its captured position

## Requirements

- Windows 10 or 11
- .NET 10 SDK (the projects target `net10.0-windows`)

## Build and run

```
dotnet build
dotnet run --project src/Sessions.App
```

or build once and run the produced `Sessions.App.exe` under
`src/Sessions.App/bin/Debug/net10.0-windows/`,

Run the test suite with:

```
dotnet test
```

## Using it

- New Session, tick the open windows to capture, give it a name, Save
- select a session and Load to reopen its windows
- Add Apps adds currently open windows to the selected session, Remove drops the selected app
- Export writes a session to a `.json` file, Import reads one back

Saved sessions live in `%APPDATA%\Sessions\sessions`, the remembered window size in
`%APPDATA%\Sessions\window.json`,

## Terminal hook (needed to capture Windows Terminal)

Capturing a terminal needs the current folder of each shell, a running shell does not expose it, so a
small PowerShell hook records it, dot-source the hook from your PowerShell profile (`$PROFILE`):

```powershell
. "C:\path\to\sessions\tools\terminal-hook\sessions-hook.ps1"
```

open a new PowerShell tab afterwards so the hook loads, it writes one record per shell under
`%APPDATA%\Sessions\terminals` and prunes dead ones on each new shell,

The hook is PowerShell only, a Command Prompt (`cmd.exe`) tab is not recorded, run Claude Code from a
PowerShell tab if you want its session captured,

## Known limits

- the generic adapter relaunches an app by its executable only, so an app that keeps its state in
  arguments, a profile, or a single running instance does not come back to the same content
- Google Chrome reopens but lands on a blank or default tab, the specific tabs and urls are not
  captured (a browser adapter is a planned addition)
- Spotify is launcher-based and single-instance, relaunching its executable is a no-op, so it is
  reported as a failed entry rather than reopening
- VS Code resolves the folder from its saved window state and then its recent-folders list, a folder
  that this VS Code has never opened, or two open folders that share the same base name, cannot be
  resolved and is reported as a failure
- Windows Terminal capture needs the hook above, and when Windows Terminal is your default terminal
  it draws shells launched from elsewhere, so two separate terminal windows are merged into one on
  restore
- a Claude Code tab with no saved session file to resume is reported as a failure rather than opening
  a fresh Claude
- an elevated (administrator) window cannot be positioned unless Sessions is also run as administrator
- a window on another virtual desktop is captured, but it reopens on the current desktop

## Layout

- `src/Sessions.Core` - models, window enumeration and placement, the adapters, the session store
- `src/Sessions.App` - the WPF app (MVVM), light and dark theme following the system setting
- `tests/Sessions.Tests` - xUnit tests for the core
- `tools/terminal-hook` - the PowerShell terminal hook
- `docs/spikes` - notes from the VS Code and terminal investigations
