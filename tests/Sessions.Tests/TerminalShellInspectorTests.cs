using Sessions.Core;

namespace Sessions.Tests;

public class TerminalShellInspectorTests
{
    [Fact]
    public void Inspect_ClaudeUnderShell_IsClaude()
    {
        ProcessSnapshot snapshot = new(new[]
        {
            new ProcessInfo(10, 1, "pwsh.exe", null),
            new ProcessInfo(11, 10, "claude.exe", "claude"),
        });

        var (isClaude, ssh) = TerminalShellInspector.Inspect(snapshot, shellPid: 10);

        Assert.True(isClaude);
        Assert.Null(ssh);
    }

    [Fact]
    public void Inspect_SshUnderShell_ReturnsItsCommandLine()
    {
        ProcessSnapshot snapshot = new(new[]
        {
            new ProcessInfo(10, 1, "cmd.exe", null),
            new ProcessInfo(12, 10, "ssh.exe", "ssh user@host -p 2222"),
        });

        var (isClaude, ssh) = TerminalShellInspector.Inspect(snapshot, shellPid: 10);

        Assert.False(isClaude);
        Assert.Equal("ssh user@host -p 2222", ssh);
    }

    [Fact]
    public void Inspect_DeepDescendant_IsStillFound()
    {
        // claude under a git pager under the shell, the walk is transitive
        ProcessSnapshot snapshot = new(new[]
        {
            new ProcessInfo(10, 1, "pwsh.exe", null),
            new ProcessInfo(11, 10, "cmd.exe", null),
            new ProcessInfo(12, 11, "claude.exe", "claude"),
        });

        var (isClaude, _) = TerminalShellInspector.Inspect(snapshot, shellPid: 10);

        Assert.True(isClaude);
    }

    [Fact]
    public void Inspect_PlainShell_IsNeither()
    {
        ProcessSnapshot snapshot = new(new[]
        {
            new ProcessInfo(10, 1, "pwsh.exe", null),
        });

        var (isClaude, ssh) = TerminalShellInspector.Inspect(snapshot, shellPid: 10);

        Assert.False(isClaude);
        Assert.Null(ssh);
    }
}
