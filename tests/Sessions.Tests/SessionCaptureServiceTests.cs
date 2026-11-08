using Sessions.Core;

namespace Sessions.Tests;

public class SessionCaptureServiceTests
{
    private static SessionCaptureService Service(params string[] existingNames)
    {
        AdapterRegistry registry = new(Array.Empty<IAdapter>(), new GenericAdapter(
            waiter: new ThrowingWaiter(),
            placer: new ThrowingPlacer()));
        return new SessionCaptureService(registry, () => existingNames);
    }

    private static WindowInfo Window(string title, string? exePath) =>
        new(Handle: 1, Title: title, ProcessId: 1) { ExePath = exePath };

    [Fact]
    public void Capture_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Service().Capture(Array.Empty<WindowInfo>(), "   "));
    }

    [Fact]
    public void Capture_DuplicateName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Service("Work").Capture(Array.Empty<WindowInfo>(), "work")); // case-insensitive
    }

    [Fact]
    public void Capture_OneFailure_DoesNotDropTheOthers()
    {
        WindowInfo good1 = Window("Good1", @"C:\a.exe");
        WindowInfo bad = Window("NoExe", exePath: null); // supported but no exe -> CaptureException
        WindowInfo good2 = Window("Good2", @"C:\b.exe");

        SessionCaptureResult result = Service().Capture(new[] { good1, bad, good2 }, "Session");

        Assert.Equal(2, result.Session.Entries.Count);
        Assert.Contains(result.Session.Entries, e => e.DisplayName == "Good1");
        Assert.Contains(result.Session.Entries, e => e.DisplayName == "Good2");

        CaptureFailure failure = Assert.Single(result.Failures);
        Assert.Equal("NoExe", failure.Window.Title);
    }

    private sealed class ThrowingWaiter : IWindowWaiter
    {
        public nint WaitForNewWindow(string exePath, string displayName) =>
            throw new InvalidOperationException();
    }

    private sealed class ThrowingPlacer : IWindowPlacer
    {
        public void Apply(nint hwnd, Placement placement, bool targetElevated) =>
            throw new InvalidOperationException();
    }
}
