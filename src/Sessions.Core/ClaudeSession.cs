using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sessions.Core;

// Finds the Claude Code session to resume for a terminal tab's working folder,
// Claude stores one .jsonl per session under a per-cwd project folder whose name
// encodes the cwd, the file name without its extension is the session id, see
// docs/spikes/terminal.md
public static class ClaudeSession
{
    // %USERPROFILE%\.claude\projects
    public static string DefaultProjectsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "projects");

    // The project-folder name Claude derives from a cwd: every character that is
    // not a-z A-Z 0-9 becomes a dash, case is preserved, e.g.
    // C:\Users\ZEPHYRUS G14\Desktop\sessions -> C--Users-ZEPHYRUS-G14-Desktop-sessions
    public static string EncodeCwd(string cwd)
    {
        StringBuilder sb = new(cwd.Length);
        foreach (char c in cwd)
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        return sb.ToString();
    }

    // The newest session id from a set of (id, lastWrite) candidates, or null when
    // there are none, newest by last-write time wins
    public static string? SelectNewestSessionId(IEnumerable<(string Id, DateTimeOffset Modified)> candidates)
    {
        return candidates
            .OrderByDescending(c => c.Modified)
            .Select(c => c.Id)
            .FirstOrDefault();
    }

    // The newest session id for the given cwd, or null when the project folder or a
    // .jsonl is missing
    public static string? FindSessionId(string projectsDirectory, string cwd)
    {
        string folder = Path.Combine(projectsDirectory, EncodeCwd(cwd));
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var candidates = Directory
            .EnumerateFiles(folder, "*.jsonl")
            .Select(f => (Id: Path.GetFileNameWithoutExtension(f),
                          Modified: (DateTimeOffset)File.GetLastWriteTimeUtc(f)));

        return SelectNewestSessionId(candidates);
    }
}
