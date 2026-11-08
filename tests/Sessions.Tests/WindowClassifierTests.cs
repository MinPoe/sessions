using Sessions.Core;

namespace Sessions.Tests;

public class WindowClassifierTests
{
    private static WindowInfo Window(string? exePath = @"C:\Program Files\App\app.exe") =>
        new(Handle: 1, Title: "A window", ProcessId: 1234) { ExePath = exePath };

    [Fact]
    public void OrdinaryApp_IsSupported()
    {
        Assert.Null(WindowClassifier.GetUnsupportedReason(Window()));
    }

    [Fact]
    public void Explorer_IsUnsupported()
    {
        WindowInfo w = Window(@"C:\Windows\explorer.exe");
        string? reason = WindowClassifier.GetUnsupportedReason(w);
        Assert.NotNull(reason);
        Assert.Contains("Explorer", reason);
    }

    [Fact]
    public void ApplicationFrameHost_IsUnsupported()
    {
        WindowInfo w = Window(@"C:\Windows\System32\ApplicationFrameHost.exe");
        string? reason = WindowClassifier.GetUnsupportedReason(w);
        Assert.NotNull(reason);
        Assert.Contains("UWP", reason);
    }

    [Fact]
    public void ExeName_MatchIsCaseInsensitive()
    {
        WindowInfo w = Window(@"C:\Windows\Explorer.EXE");
        Assert.NotNull(WindowClassifier.GetUnsupportedReason(w));
    }

    [Fact]
    public void Elevated_IsUnsupported_RegardlessOfExe()
    {
        WindowInfo w = Window() with { IsElevated = true };
        string? reason = WindowClassifier.GetUnsupportedReason(w);
        Assert.NotNull(reason);
        Assert.Contains("elevated", reason);
    }

    [Fact]
    public void NullExePath_CarriesForwardExistingReason()
    {
        WindowInfo w = new(Handle: 1, Title: "x", ProcessId: 1)
        {
            ExePath = null,
            UnsupportedReason = "original reason",
        };
        Assert.Equal("original reason", WindowClassifier.GetUnsupportedReason(w));
    }
}
