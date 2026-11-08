using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sessions.Core;

// A live shell as reported by the PowerShell hook (tools/terminal-hook), one file
// per pid under %APPDATA%\Sessions\terminals, see docs/spikes/terminal.md
public sealed record TerminalRecord(
    int Pid,
    string Cwd,
    string ShellPath,
    string? WtSession,
    DateTimeOffset Updated);

// Reads the hook's records and narrows them to the shells still running, the reader
// skips files it can't parse so one stale file never sinks a capture
public static class TerminalRecords
{
    // %APPDATA%\Sessions\terminals
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sessions", "terminals");

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<TerminalRecord> Read(string directory)
    {
        List<TerminalRecord> records = new();
        if (!Directory.Exists(directory))
        {
            return records;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                TerminalRecord? record = JsonSerializer.Deserialize<TerminalRecord>(File.ReadAllText(file), Options);
                if (record is not null && !string.IsNullOrEmpty(record.Cwd))
                {
                    records.Add(record);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // A half-written or stale file, skip it
            }
        }

        return records;
    }

    // Keeps the records whose pid is still alive, dropping stale files left by shells
    // that have since closed, the live records are the terminal's tabs
    //
    // Note, we do not filter by descent from the WindowsTerminal.exe window, when
    // Windows Terminal is the default terminal it draws consoles that were launched
    // elsewhere (parent is explorer.exe, no WT_SESSION), so a handed-off shell is not
    // a process descendant of the terminal window and has no window-linking signal we
    // can read from outside, see docs/spikes/terminal.md
    public static IReadOnlyList<TerminalRecord> LiveShells(
        IEnumerable<TerminalRecord> records,
        ProcessSnapshot snapshot)
    {
        return records
            .Where(r => snapshot.Contains((uint)r.Pid))
            .OrderBy(r => r.Pid)
            .ToList();
    }
}
