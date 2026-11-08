# VS Code spike

Findings behind the VS Code adapter, gathered on 2026-11-08 from the storage file on this machine (VS Code stable, single window open on the `sessions` folder)

## Where the window state lives

`%APPDATA%\Code\User\globalStorage\storage.json`, a single JSON object, the relevant key is `windowsState`:

```json
{
  "windowsState": {
    "lastActiveWindow": {
      "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions",
      "backupPath": "C:\\Users\\...\\Code\\Backups\\<hash>",
      "uiState": { "mode": 0, "x": 548, "y": 87, "width": 1200, "height": 800 }
    },
    "openedWindows": []
  }
}
```

- `windowsState.lastActiveWindow` — the window focused at the last write
- `windowsState.openedWindows` — an array of the other windows, observed empty with a single window open (see the two-window note below)
- Each window entry carries exactly one of:
  - `folder` — a `file://` URI of an open folder, percent-encoded (drive colon is `%3A`, space is `%20`)
  - `workspace` — an object `{ "id": "...", "configPath": "file:///.../name.code-workspace" }` for a multi-root or saved workspace, the `configPath` URI points at the `.code-workspace` file
  - a window with no folder and no workspace is an empty window, nothing to restore
- `uiState` holds the geometry, we do not use it (see currency), live geometry comes from `GetWindowPlacement` at capture time instead

### Decoding a `file://` folder URI

`file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions` resolves to `c:\Users\ZEPHYRUS G14\Desktop\sessions`:
- strip the leading `file://` then the leading `/` before the drive
- percent-decode (`%3A` to `:`, `%20` to space, etc.)
- swap `/` for `\`

## Is it current while VS Code runs?

No, it is not live, the file was last written at 22:37 while VS Code was still running at 22:52, a 15-minute lag, VS Code writes `windowsState` when a window opens or closes and on a clean exit, not continuously

Consequences for the adapter:
- the folder/workspace binding for a window that is currently open is reliable, because the file was written when that window opened
- `uiState` geometry is stale and must not be trusted, we read live position from the window handle instead
- a window opened after the last write will not appear until the next open/close/exit, in practice every open window triggered a write when it opened, so open windows are represented

## Title format for open folders

Default title template is `${activeEditorShort}${separator}${rootName}${separator}${appName}`, rendering as:

```
AdapterRegistry.cs - sessions - Visual Studio Code
```

- the segment before ` - Visual Studio Code` is `rootName`, which is the open folder's base name (`sessions` for the folder above)
- for a `.code-workspace`, `rootName` renders as `<workspaceName> (Workspace)`
- with two folders open, each window's title ends `- <folderBaseName> - Visual Studio Code`, so the base name is what ties a window to one `windowsState` entry

Matching plan for the parser: take `rootName` from the title, then find the `windowsState` entry whose decoded folder base name (or workspace file base name) equals it

### Edge cases to handle

- two open folders with the same base name collide on `rootName`, the title alone cannot disambiguate them, fall back to leaving the path unresolved rather than guessing
- a custom `window.title` setting changes the template, so a parse that depends on the exact ` - <root> - Visual Studio Code` shape can miss, treat a non-match as unresolved
- the two-window `openedWindows` layout was later confirmed with a live two-window session: `openedWindows` lists every open window including the active one, so `lastActiveWindow` is repeated there, a session with `sessions` and `eth-uvm-tb` open showed `eth-uvm-tb` as both `lastActiveWindow` and an `openedWindows` element, the parser reads both lists uniformly but keeps each path once, otherwise the repeated folder looks like two windows sharing a `rootName` and every title matching it resolves as ambiguous and fails to capture
