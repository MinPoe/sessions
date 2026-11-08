using Sessions.Core;

namespace Sessions.Tests;

public class GenericAdapterTests
{
    private readonly GenericAdapter _adapter = new(
        waiter: new UnusedWaiter(),
        placer: new UnusedPlacer());

    [Fact]
    public void Capture_BuildsGenericEntry_WithArgsStrippedOfExe()
    {
        WindowInfo window = new(Handle: 1, Title: "Notepad", ProcessId: 10)
        {
            ExePath = @"C:\Windows\notepad.exe",
            CommandLine = "\"C:\\Windows\\notepad.exe\" C:\\notes.txt",
            Placement = new Placement(10, 20, 300, 400, 1),
        };

        GenericEntry entry = Assert.IsType<GenericEntry>(_adapter.Capture(window));

        Assert.Equal("Notepad", entry.DisplayName);
        Assert.Equal(@"C:\Windows\notepad.exe", entry.ExePath);
        Assert.Equal(@"C:\notes.txt", entry.Arguments);
        Assert.Equal(new Placement(10, 20, 300, 400, 1), entry.Placement);
    }

    [Fact]
    public void Capture_NoArguments_YieldsEmptyArgs()
    {
        WindowInfo window = new(Handle: 1, Title: "App", ProcessId: 10)
        {
            ExePath = @"C:\app.exe",
            CommandLine = "\"C:\\app.exe\"",
        };

        GenericEntry entry = Assert.IsType<GenericEntry>(_adapter.Capture(window));

        Assert.Equal(string.Empty, entry.Arguments);
    }

    [Fact]
    public void Capture_MissingExePath_Throws()
    {
        WindowInfo window = new(Handle: 1, Title: "Mystery", ProcessId: 10) { ExePath = null };

        Assert.Throws<CaptureException>(() => _adapter.Capture(window));
    }

    [Fact]
    public void CanHandle_FollowsSupportedState()
    {
        WindowInfo supported = new(Handle: 1, Title: "ok", ProcessId: 1) { ExePath = @"C:\a.exe" };
        WindowInfo unsupported = supported with { UnsupportedReason = "nope" };

        Assert.True(_adapter.CanHandle(supported));
        Assert.False(_adapter.CanHandle(unsupported));
    }

    private sealed class UnusedWaiter : IWindowWaiter
    {
        public nint WaitForNewWindow(string exePath, string displayName) =>
            throw new InvalidOperationException("should not be called in capture tests");
    }

    private sealed class UnusedPlacer : IWindowPlacer
    {
        public void Apply(nint hwnd, Placement placement, bool targetElevated) =>
            throw new InvalidOperationException("should not be called in capture tests");
    }
}
