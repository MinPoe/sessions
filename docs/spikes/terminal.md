# Spike: Windows Terminal and Claude Code

What a terminal entry needs to capture and restore a Windows Terminal window, its tabs,
their working folders, and any Claude Code or SSH session running inside them,
findings gathered on this machine (Windows 11, Windows Terminal 1.24, Claude Code native launcher)

## Process tree under WindowsTerminal.exe

- `WindowsTerminal.exe` is the UI host, it is launched by COM activation so its own command line is `"...WindowsTerminal.exe" -Embedding` and its parent is a service, not a useful anchor
- each pane gets an `OpenConsole.exe` conpty host, also launched `-Embedding`, its parent is the same service rather than `WindowsTerminal.exe`, so OpenConsole is not on the shell's parent chain
- a shell opened from inside Windows Terminal (the `+` button, a profile) is spawned by Windows Terminal and gets `WT_SESSION` in its environment, for that shell the parent chain does lead back toward the terminal

Correction (confirmed later with a live session, it overturns the first assumption):
walking `ParentProcessId` from a shell up to `WindowsTerminal.exe` is **not** reliable, when Windows
Terminal is the default terminal application it draws consoles that were launched elsewhere, a
`powershell.exe` started from Explorer or the taskbar is handed off to `WindowsTerminal.exe` to
render, its window then belongs to `WindowsTerminal.exe` (so the window is captured) but
- its process parent is `explorer.exe`, not the terminal, so a descendant walk from the terminal PID never reaches it
- it has no `WT_SESSION`, because Windows Terminal did not spawn it

observed, a Claude Code session in such a handed-off `powershell.exe` (pid A, parent `explorer.exe`)
with `claude.exe` as its child, displayed in `WindowsTerminal.exe` whose window title Claude had set,
the only link from that shell to its terminal window lives inside Windows Terminal's conpty
connection, there is no window-to-shell signal readable from outside

so the capture rule is: the `WindowsTerminal.exe` window is only the trigger, take every live shell
the hook recorded (drop records whose PID has died), do not filter by descent from the terminal
window, the tradeoff is that two separate terminal windows merge into one on restore

Still useful: `WT_SESSION` (a GUID) and `WT_PROFILE_ID` are present for Windows-Terminal-spawned
shells and absent for handed-off or bare consoles, the hook records `WT_SESSION` for later use, it is
not a reliable per-window key because handed-off shells lack it

## The claude process

- process name is `claude.exe`, the command line is literally `claude` with no arguments
- it is the native launcher (resolved through the `~/.local/bin/claude` shim), not a `node.exe` host, so the tab is Claude Code when a `claude.exe` descendant exists, no need to inspect node command lines
- detection is just the presence of a `claude.exe` among the shell's descendants

## Claude session files and cwd encoding

- sessions live at `%USERPROFILE%\.claude\projects\<encoded-cwd>\<session-id>.jsonl`
- the `.jsonl` file name without its extension is the session id, so the newest file in the folder gives both the most recent session and the id to resume with, no need to read the file contents
- cwd encoding: replace every character that is not `a-z A-Z 0-9` with `-`, case is preserved, it is one-way (dashes in the original and the separators become indistinguishable) but one-way is all we need, we only ever go cwd to folder
  - `C:\Users\ZEPHYRUS G14\Desktop\sessions` becomes `C--Users-ZEPHYRUS-G14-Desktop-sessions` (`:` and `\` and space each map to `-`)
  - a `~` or a `.` in the path maps to `-` the same way
- newest by last-write time is the selection rule, a cwd with no matching folder, or a folder with no `.jsonl`, means no resumable session

## claude --resume

- `claude --resume <session-id>` (alias `-r`) resumes that exact conversation, the help text reads "`claude --resume <id>` opens it again"
- `--continue` / `-c` continues the most recent conversation for the terminal's directory, step 40 does not use it, a Claude tab with no session file found is a capture failure rather than a silent `--continue`
- so a Claude tab restores by launching the shell and running `claude --resume <sessionId>` in the captured cwd

## wt command line, the ; separator and -w new

- `wt.exe` parses its own command line and splits subcommands on `;`, each `;` starts a new `new-tab` (or `split-pane`) subcommand
- when we launch `wt.exe` directly through `Process.Start` (a `CreateProcess`, no shell in between) the `;` is handed to wt literally and separates tabs as intended, the backtick-escaping dance (`` `; ``) only matters when wt is invoked through PowerShell, which interprets `;` first, we avoid that by starting `wt.exe` ourselves
- keep spaces around the `;` (`... ; new-tab ...`) for readability, wt accepts it either way
- `-w new` (`--window new`) opens the whole layout in a brand new terminal window, `new` is a reserved window name, `-w 0` would instead target the current window, every restore uses `-w new` so it never piles tabs into an existing window
- per-tab directory is `-d "<cwd>"`, the shell executable follows as the command to run, a shell command uses the shell's own run flag (`-NoExit -Command "<cmd>"` for PowerShell), paths with spaces are double-quoted
