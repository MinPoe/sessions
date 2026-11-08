using System.Linq;

namespace Sessions.Core;

// Looks at what a shell is running to decide how to restore its tab, from the
// shell's process descendants, see docs/spikes/terminal.md
public static class TerminalShellInspector
{
    private const string ClaudeProcessName = "claude.exe";
    private const string SshProcessName = "ssh.exe";

    // IsClaude is true when a claude.exe runs under the shell, SshCommand is the
    // command line of an ssh.exe under the shell (so the tab can re-run it) or null
    public static (bool IsClaude, string? SshCommand) Inspect(ProcessSnapshot snapshot, uint shellPid)
    {
        bool isClaude = false;
        string? sshCommand = null;

        foreach (ProcessInfo descendant in snapshot.Descendants(shellPid))
        {
            if (Matches(descendant.Name, ClaudeProcessName))
            {
                isClaude = true;
            }
            else if (sshCommand is null && Matches(descendant.Name, SshProcessName))
            {
                sshCommand = descendant.CommandLine;
            }
        }

        return (isClaude, sshCommand);
    }

    private static bool Matches(string name, string target) =>
        string.Equals(name, target, StringComparison.OrdinalIgnoreCase);
}
