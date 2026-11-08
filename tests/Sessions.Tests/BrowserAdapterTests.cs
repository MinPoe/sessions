using System;
using System.Collections.Generic;
using Sessions.Core;

namespace Sessions.Tests;

public class BrowserAdapterTests
{
    private static readonly BrowserWindow[] TwoWindows =
    {
        new(Id: 1, Focused: true, Title: "MinPoe/sessions", Urls: new[] { "https://github.com/MinPoe/sessions" }),
        new(Id: 2, Focused: false, Title: "Hacker News", Urls: new[] { "https://news.ycombinator.com", "https://example.com" }),
    };

    private static BrowserAdapter Adapter(Func<IReadOnlyList<BrowserWindow>> query) =>
        new(query, new NoWaiter(), new NoPlacer(), _ => { }, () => DateTime.UtcNow);

    private static WindowInfo ChromeWindow(string title) =>
        new(Handle: 1, Title: title, ProcessId: 10)
        {
            ExePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Placement = new Placement(0, 0, 1400, 900, 1),
        };

    [Fact]
    public void CanHandle_Chrome_IsTrue()
    {
        Assert.True(Adapter(() => TwoWindows).CanHandle(ChromeWindow("x - Google Chrome")));
    }

    [Fact]
    public void CanHandle_OtherExe_IsFalse()
    {
        WindowInfo w = ChromeWindow("x") with { ExePath = @"C:\Windows\notepad.exe" };
        Assert.False(Adapter(() => TwoWindows).CanHandle(w));
    }

    [Fact]
    public void Capture_MatchesWindowByActiveTabTitle()
    {
        Entry entry = Adapter(() => TwoWindows).Capture(ChromeWindow("Hacker News - Google Chrome"));

        BrowserEntry browser = Assert.IsType<BrowserEntry>(entry);
        Assert.Equal("browser", browser.AdapterId);
        Assert.Equal(new[] { "https://news.ycombinator.com", "https://example.com" }, browser.Urls);
        Assert.Equal(new Placement(0, 0, 1400, 900, 1), browser.Placement);
    }

    [Fact]
    public void Capture_SingleWindow_FallsBackWithoutTitleMatch()
    {
        BrowserWindow[] one = { new(Id: 9, Focused: true, Title: "whatever", Urls: new[] { "https://only.example" }) };
        Entry entry = Adapter(() => one).Capture(ChromeWindow("a stale title - Google Chrome"));

        Assert.Equal(new[] { "https://only.example" }, Assert.IsType<BrowserEntry>(entry).Urls);
    }

    [Fact]
    public void Capture_AmbiguousNoMatch_Throws()
    {
        Assert.Throws<CaptureException>(() =>
            Adapter(() => TwoWindows).Capture(ChromeWindow("unknown page - Google Chrome")));
    }

    [Fact]
    public void Capture_ExtensionUnavailable_Throws()
    {
        Assert.Throws<CaptureException>(() =>
            Adapter(() => throw new BrowserUnavailableException("no host")).Capture(ChromeWindow("x - Google Chrome")));
    }

    [Fact]
    public void Capture_QueriesOnce_AndCachesAcrossWindows()
    {
        int calls = 0;
        BrowserAdapter adapter = Adapter(() => { calls++; return TwoWindows; });

        adapter.Capture(ChromeWindow("MinPoe/sessions - Google Chrome"));
        adapter.Capture(ChromeWindow("Hacker News - Google Chrome"));

        Assert.Equal(1, calls); // the second window reused the cached query
    }

    [Fact]
    public void BuildArguments_QuotesEachUrlAfterNewWindow()
    {
        string args = BrowserAdapter.BuildArguments(new[] { "https://a.example", "https://b.example" });
        Assert.Equal("--new-window \"https://a.example\" \"https://b.example\"", args);
    }

    [Fact]
    public void Registry_SelectsBrowser_BeforeGeneric()
    {
        AdapterRegistry registry = new(new IAdapter[] { Adapter(() => TwoWindows) }, new GenericAdapter());
        Assert.Equal("browser", registry.SelectForCapture(ChromeWindow("x - Google Chrome"))?.Id);
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
