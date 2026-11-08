using Sessions.Core;

namespace Sessions.Tests;

public class WtCommandBuilderTests
{
    private static TerminalEntry Entry(params TerminalTab[] tabs) =>
        new("Terminal", Placement: null, tabs);

    [Fact]
    public void Build_SingleShellTab_OpensNewWindowInCwd()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\work", @"C:\pwsh.exe", Command: null, ClaudeSessionId: null)));

        Assert.Equal("-w new -d \"C:\\work\" \"C:\\pwsh.exe\"", args);
    }

    [Fact]
    public void Build_QuotesPathsWithSpaces()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\Users\ZEPHYRUS G14\Desktop\sessions", @"C:\Program Files\pwsh.exe", null, null)));

        Assert.Contains("-d \"C:\\Users\\ZEPHYRUS G14\\Desktop\\sessions\"", args);
        Assert.Contains("\"C:\\Program Files\\pwsh.exe\"", args);
    }

    [Fact]
    public void Build_MultipleTabs_JoinsWithNewTabSeparator()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\a", @"C:\pwsh.exe", null, null),
            new TerminalTab(@"C:\b", @"C:\pwsh.exe", null, null)));

        Assert.Equal(
            "-w new -d \"C:\\a\" \"C:\\pwsh.exe\" ; new-tab -d \"C:\\b\" \"C:\\pwsh.exe\"",
            args);
    }

    [Fact]
    public void Build_ClaudeTab_ResumesSession()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\proj", @"C:\pwsh.exe", Command: null, ClaudeSessionId: "abc-123")));

        Assert.Equal(
            "-w new -d \"C:\\proj\" \"C:\\pwsh.exe\" -NoExit -Command \"claude --resume abc-123\"",
            args);
    }

    [Fact]
    public void Build_SshTab_ReRunsCommand()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\proj", @"C:\pwsh.exe", Command: "ssh user@host", ClaudeSessionId: null)));

        Assert.Contains("-NoExit -Command \"ssh user@host\"", args);
    }

    [Fact]
    public void Build_CmdShell_UsesSlashKToRunCommand()
    {
        string args = WtCommandBuilder.Build(Entry(
            new TerminalTab(@"C:\proj", @"C:\Windows\System32\cmd.exe", Command: "ssh user@host", ClaudeSessionId: null)));

        Assert.Contains("/k \"ssh user@host\"", args);
    }
}
