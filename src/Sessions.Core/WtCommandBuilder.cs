using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sessions.Core;

// Builds the wt.exe argument line that reopens a terminal window and its tabs,
// we start wt.exe directly (no shell) so the ; tab separators pass through
// literally, see docs/spikes/terminal.md
public static class WtCommandBuilder
{
    // -w new opens one fresh window, the first tab is implicit, each later tab is a
    // "; new-tab" subcommand, a Claude tab resumes its session, an ssh tab re-runs
    // its command
    public static string Build(TerminalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        StringBuilder sb = new("-w new");

        bool first = true;
        foreach (TerminalTab tab in entry.Tabs)
        {
            sb.Append(first ? " " : " ; new-tab ");
            sb.Append(TabArguments(tab));
            first = false;
        }

        return sb.ToString();
    }

    private static string TabArguments(TerminalTab tab)
    {
        StringBuilder sb = new();
        sb.Append("-d ").Append(Quote(tab.Cwd));
        sb.Append(' ').Append(Quote(tab.ShellPath));

        string? command = InnerCommand(tab);
        if (command is not null)
        {
            sb.Append(' ').Append(ShellRun(tab.ShellPath, command));
        }

        return sb.ToString();
    }

    // What to run inside the tab's shell: resume Claude, else an ssh command, else
    // nothing (just open the shell in the folder)
    private static string? InnerCommand(TerminalTab tab)
    {
        if (!string.IsNullOrEmpty(tab.ClaudeSessionId))
        {
            return $"claude --resume {tab.ClaudeSessionId}";
        }

        return string.IsNullOrEmpty(tab.Command) ? null : tab.Command;
    }

    // The shell-specific way to run a command and stay open, cmd keeps the window
    // with /k, PowerShell with -NoExit -Command
    private static string ShellRun(string shellPath, string command)
    {
        string shell = Path.GetFileName(shellPath);
        return string.Equals(shell, "cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? $"/k {Quote(command)}"
            : $"-NoExit -Command {Quote(command)}";
    }

    private static string Quote(string value) => $"\"{value}\"";
}
