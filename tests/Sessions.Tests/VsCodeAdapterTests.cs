using Sessions.Core;

namespace Sessions.Tests;

public class VsCodeAdapterTests
{
    private const string FolderStorage =
        """
        {
          "windowsState": {
            "lastActiveWindow": { "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions" },
            "openedWindows": []
          }
        }
        """;

    private static VsCodeAdapter Adapter(string? storageJson, string? recentJson = null) =>
        new(() => storageJson, () => recentJson, new NoWaiter(), new NoPlacer(), _ => { });

    private static WindowInfo CodeWindow(string title) =>
        new(Handle: 1, Title: title, ProcessId: 10)
        {
            ExePath = @"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\Code.exe",
            Placement = new Placement(100, 100, 1200, 800, 1),
        };

    [Fact]
    public void CanHandle_CodeExe_IsTrue()
    {
        Assert.True(Adapter(FolderStorage).CanHandle(CodeWindow("x - sessions - Visual Studio Code")));
    }

    [Fact]
    public void CanHandle_OtherExe_IsFalse()
    {
        WindowInfo notCode = new(Handle: 1, Title: "x", ProcessId: 1) { ExePath = @"C:\Windows\notepad.exe" };
        Assert.False(Adapter(FolderStorage).CanHandle(notCode));
    }

    [Fact]
    public void CanHandle_UnsupportedWindow_IsFalse()
    {
        WindowInfo unsupported = CodeWindow("x - sessions - Visual Studio Code") with { UnsupportedReason = "nope" };
        Assert.False(Adapter(FolderStorage).CanHandle(unsupported));
    }

    [Fact]
    public void Capture_ResolvesFolder_IntoVsCodeEntry()
    {
        Entry entry = Adapter(FolderStorage).Capture(CodeWindow("AdapterRegistry.cs - sessions - Visual Studio Code"));

        VsCodeEntry vs = Assert.IsType<VsCodeEntry>(entry);
        Assert.Equal(@"c:\Users\ZEPHYRUS G14\Desktop\sessions", vs.Path);
        Assert.Equal("sessions", vs.DisplayName);
        Assert.Equal("vscode", vs.AdapterId);
        Assert.Equal(new Placement(100, 100, 1200, 800, 1), vs.Placement);
    }

    [Fact]
    public void Capture_UnresolvedTitle_Throws()
    {
        Assert.Throws<CaptureException>(() =>
            Adapter(FolderStorage).Capture(CodeWindow("x - other-folder - Visual Studio Code")));
    }

    [Fact]
    public void Capture_NoStorage_Throws()
    {
        Assert.Throws<CaptureException>(() =>
            Adapter(storageJson: null).Capture(CodeWindow("x - sessions - Visual Studio Code")));
    }

    [Fact]
    public void Capture_StorageStale_FallsBackToRecentList()
    {
        // windowsState only knows eth-uvm-tb, but the recent list has sessions, so a
        // sessions window still resolves
        const string recent =
            """
            {
              "entries": [
                { "folderUri": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/eth-uvm-tb" },
                { "folderUri": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions" },
                { "fileUri": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions/README.md" }
              ]
            }
            """;
        const string staleStorage =
            """
            { "windowsState": { "lastActiveWindow": { "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/eth-uvm-tb" }, "openedWindows": [] } }
            """;

        Entry entry = Adapter(staleStorage, recent)
            .Capture(CodeWindow("vscode.md - sessions - Visual Studio Code"));

        VsCodeEntry vs = Assert.IsType<VsCodeEntry>(entry);
        Assert.Equal(@"c:\Users\ZEPHYRUS G14\Desktop\sessions", vs.Path);
    }

    [Fact]
    public void Registry_SelectsVsCode_BeforeGeneric_ForCodeExe()
    {
        AdapterRegistry registry = new(new IAdapter[] { Adapter(FolderStorage) }, new GenericAdapter());
        IAdapter? chosen = registry.SelectForCapture(CodeWindow("x - sessions - Visual Studio Code"));
        Assert.Equal("vscode", chosen?.Id);
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
