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

## Chrome tabs (optional)

To capture and reopen the tabs of a Chrome window, set up the browser extension and its native host,
without this Chrome still captures but reopens on a blank page, see docs/spikes/browser.md

1. build the host, `dotnet build src/Sessions.BrowserHost`
2. register it, `powershell -ExecutionPolicy Bypass -File tools\browser-host\install.ps1`, this writes
   the host manifest and an HKCU registry key, and prints the extension id
3. load the `extension` folder as an unpacked extension at `chrome://extensions` (Developer mode on),
   confirm its id matches the one the install script printed
4. capture a session with a Chrome window open, its tabs are saved, Load reopens them in one window

remove it later with `tools\browser-host\uninstall.ps1` and by removing the unpacked extension

## Known limits

- the generic adapter relaunches an app by its executable only, so an app that keeps its state in
  arguments, a profile, or a single running instance does not come back to the same content
- Google Chrome reopens its tabs only when the browser extension and native host are installed (see
  above), without them Chrome lands on a blank page, and even with them two Chrome windows showing the
  same page cannot be told apart and are reported as a failure
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
- `src/Sessions.BrowserHost` - the Chrome native messaging host (a thin stdio-to-pipe relay)
- `tests/Sessions.Tests` - xUnit tests for the core
- `tools/terminal-hook` - the PowerShell terminal hook
- `tools/browser-host` - install and uninstall scripts for the Chrome host
- `extension` - the MV3 Chrome extension that reports open tab urls
- `docs/spikes` - notes from the VS Code, terminal, and browser investigations
