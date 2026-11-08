using System;
using System.Collections.Generic;
using System.Linq;
using Sessions.Core;

namespace Sessions.Tests;

public class TerminalAdapterTests
{
    // wt(100) with a claude pwsh(101) and a plain cmd(103) under it
    private static ProcessSnapshot Snapshot() => new(new[]
    {
        new ProcessInfo(100, 1, "WindowsTerminal.exe", null),
        new ProcessInfo(101, 100, "pwsh.exe", null),
        new ProcessInfo(102, 101, "claude.exe", "claude"),
        new ProcessInfo(103, 100, "cmd.exe", null),
    });

    private static readonly TerminalRecord[] Shells =
    {
        new(101, @"C:\proj", @"C:\pwsh.exe", WtSession: "g", Updated: DateTimeOffset.UnixEpoch),
        new(103, @"C:\other", @"C:\Windows\System32\cmd.exe", WtSession: "g", Updated: DateTimeOffset.UnixEpoch),
    };

    private static WindowInfo WtWindow() =>
        new(Handle: 1, Title: "Windows Terminal", ProcessId: 100)
        {
            ExePath = @"C:\Program Files\WindowsApps\x\WindowsTerminal.exe",
            Placement = new Placement(0, 0, 1000, 700, 1),
        };

    private static TerminalAdapter Adapter(Func<string, string?>? findSession = null) =>
        new(
            Snapshot,
            () => Shells,
            findSession ?? (_ => "session-42"),
            new NoWaiter(),
            new NoPlacer(),
            _ => { });

    [Fact]
    public void CanHandle_WindowsTerminal_IsTrue()
    {
        Assert.True(Adapter().CanHandle(WtWindow()));
    }

    [Fact]
    public void CanHandle_OtherExe_IsFalse()
    {
        WindowInfo other = WtWindow() with { ExePath = @"C:\Windows\explorer.exe" };
        Assert.False(Adapter().CanHandle(other));
    }

    [Fact]
    public void Capture_BuildsTabForEachShell_WithClaudeAndPlain()
    {
        Entry entry = Adapter().Capture(WtWindow());

        TerminalEntry terminal = Assert.IsType<TerminalEntry>(entry);
        Assert.Equal("terminal", terminal.AdapterId);
        Assert.Equal(new Placement(0, 0, 1000, 700, 1), terminal.Placement);
        Assert.Equal(2, terminal.Tabs.Count);

        TerminalTab claudeTab = terminal.Tabs.Single(t => t.Cwd == @"C:\proj");
        Assert.Equal("session-42", claudeTab.ClaudeSessionId);
        Assert.Null(claudeTab.Command);

        TerminalTab plainTab = terminal.Tabs.Single(t => t.Cwd == @"C:\other");
        Assert.Null(plainTab.ClaudeSessionId);
        Assert.Null(plainTab.Command);
    }

    [Fact]
    public void Capture_ClaudeRunningButNoSessionFile_Throws()
    {
        Assert.Throws<CaptureException>(() => Adapter(findSession: _ => null).Capture(WtWindow()));
    }

    [Fact]
    public void Capture_NoRecordedShells_Throws()
    {
        TerminalAdapter adapter = new(
            Snapshot,
            () => Array.Empty<TerminalRecord>(),
            _ => "x",
            new NoWaiter(),
            new NoPlacer(),
            _ => { });

        Assert.Throws<CaptureException>(() => adapter.Capture(WtWindow()));
    }

    [Fact]
    public void Registry_SelectsTerminal_BeforeGeneric()
    {
        AdapterRegistry registry = new(new IAdapter[] { Adapter() }, new GenericAdapter());
        IAdapter? chosen = registry.SelectForCapture(WtWindow());
        Assert.Equal("terminal", chosen?.Id);
    }

    private sealed class NoWaiter : IWindowWaiter
    {
        public nint WaitForNewWindow(string exePath, string displayName) => throw new InvalidOperationException();
    }

    private sealed class NoPlacer : IWindowPlacer
    {
        public void Apply(nint hwnd, Placement placement, bool targetElevated) => throw new InvalidOperationException();
    }
}
