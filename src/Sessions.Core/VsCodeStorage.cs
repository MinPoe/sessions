using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sessions.Core;

// One open VS Code window drawn from windowsState: a folder or a .code-workspace file
public sealed record VsCodeWindow(string Path, bool IsWorkspace)
{
    // The name VS Code shows as rootName in the window title, used to match a title to this window
    public string DisplayName => IsWorkspace
        ? System.IO.Path.GetFileNameWithoutExtension(Path) + " (Workspace)"
        : System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
}

// Pure helpers for resolving which folder (or workspace file) a VS Code window has open,
// from the globalStorage windowsState plus the window title, see docs/spikes/vscode.md
public static class VsCodeStorage
{
    private const string TitleSuffix = " - Visual Studio Code";

    // Reads windowsState from a storage.json body into a flat list of open windows,
    // lastActiveWindow and every openedWindows entry are treated uniformly, empty
    // windows (no folder, no workspace) are skipped, the same path is kept once since
    // openedWindows repeats lastActiveWindow (so a title matches at most one window)
    public static IReadOnlyList<VsCodeWindow> ParseWindows(string storageJson)
    {
        List<VsCodeWindow> windows = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        using JsonDocument doc = JsonDocument.Parse(storageJson);
        if (!doc.RootElement.TryGetProperty("windowsState", out JsonElement state))
        {
            return windows;
        }

        if (state.TryGetProperty("lastActiveWindow", out JsonElement last) && last.ValueKind == JsonValueKind.Object)
        {
            AddWindow(windows, seen, last);
        }

        if (state.TryGetProperty("openedWindows", out JsonElement opened) && opened.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement window in opened.EnumerateArray())
            {
                AddWindow(windows, seen, window);
            }
        }

        return windows;
    }

    private static void AddWindow(List<VsCodeWindow> windows, HashSet<string> seen, JsonElement window)
    {
        if (window.TryGetProperty("folder", out JsonElement folder) && folder.ValueKind == JsonValueKind.String)
        {
            string? path = DecodeFileUri(folder.GetString());
            if (path is not null && seen.Add("folder:" + path))
            {
                windows.Add(new VsCodeWindow(path, IsWorkspace: false));
            }

            return;
        }

        if (window.TryGetProperty("workspace", out JsonElement workspace) &&
            workspace.ValueKind == JsonValueKind.Object &&
            workspace.TryGetProperty("configPath", out JsonElement configPath) &&
            configPath.ValueKind == JsonValueKind.String)
        {
            string? path = DecodeFileUri(configPath.GetString());
            if (path is not null && seen.Add("workspace:" + path))
            {
                windows.Add(new VsCodeWindow(path, IsWorkspace: true));
            }
        }

        // Otherwise an empty window, nothing to restore
    }

    // Reads VS Code's recently opened list (history.recentlyOpenedPathsList from
    // state.vscdb) into open-window candidates, this list is more complete and more
    // current than windowsState which is written lazily and is often stale, remote
    // entries (vscode-remote://, non file URIs) and plain recent files are skipped
    // since we can only reopen a local folder or workspace, duplicates kept once
    public static IReadOnlyList<VsCodeWindow> ParseRecentFolders(string recentJson)
    {
        List<VsCodeWindow> windows = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        using JsonDocument doc = JsonDocument.Parse(recentJson);
        if (!doc.RootElement.TryGetProperty("entries", out JsonElement entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return windows;
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("folderUri", out JsonElement folderUri) &&
                folderUri.ValueKind == JsonValueKind.String)
            {
                string? path = DecodeFileUri(folderUri.GetString());
                if (path is not null && seen.Add("folder:" + path))
                {
                    windows.Add(new VsCodeWindow(path, IsWorkspace: false));
                }
            }
            else if (entry.TryGetProperty("workspace", out JsonElement workspace) &&
                     workspace.ValueKind == JsonValueKind.Object &&
                     workspace.TryGetProperty("configPath", out JsonElement configPath) &&
                     configPath.ValueKind == JsonValueKind.String)
            {
                string? path = DecodeFileUri(configPath.GetString());
                if (path is not null && seen.Add("workspace:" + path))
                {
                    windows.Add(new VsCodeWindow(path, IsWorkspace: true));
                }
            }

            // fileUri entries are recently opened files, not folders, skip them
        }

        return windows;
    }

    // Resolves the folder or workspace path the given window title refers to, or null
    // when nothing matches or two candidates share the title's root name (ambiguous)
    public static string? Resolve(string windowTitle, IReadOnlyList<VsCodeWindow> windows)
    {
        string? rootName = RootNameFromTitle(windowTitle);
        if (rootName is null)
        {
            return null;
        }

        List<VsCodeWindow> matches = windows
            .Where(w => string.Equals(w.DisplayName, rootName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0].Path : null;
    }

    // The rootName segment of a default VS Code title, the part just before
    // " - Visual Studio Code", null if the title isn't in that shape (custom template)
    public static string? RootNameFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title) || !title.EndsWith(TitleSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        string head = title[..^TitleSuffix.Length].Trim();
        if (head.Length == 0)
        {
            return null;
        }

        // Template is "${activeEditorShort} - ${rootName}", rootName is the last " - " segment,
        // or the whole head when no file is open ("sessions - Visual Studio Code")
        int lastSep = head.LastIndexOf(" - ", StringComparison.Ordinal);
        string rootName = lastSep >= 0 ? head[(lastSep + 3)..] : head;
        return rootName.Length == 0 ? null : rootName;
    }

    // Decodes a VS Code file:// URI to a Windows path, e.g.
    // file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions -> c:\Users\ZEPHYRUS G14\Desktop\sessions
    public static string? DecodeFileUri(string? uri)
    {
        const string prefix = "file://";
        if (string.IsNullOrEmpty(uri) || !uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string rest = uri[prefix.Length..];

        // A local path is file:///<drive>..., a UNC path is file://server/share
        if (rest.StartsWith('/'))
        {
            string decoded = Uri.UnescapeDataString(rest[1..]);
            return decoded.Replace('/', '\\');
        }

        return @"\\" + Uri.UnescapeDataString(rest).Replace('/', '\\');
    }
}
