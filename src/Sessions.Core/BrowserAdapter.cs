using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sessions.Core;

// Captures and restores a Chrome window with its tabs, capture asks the extension
// for every window's tabs over the pipe (once, cached for the batch) and matches
// this OS window to one of them by its active tab title, restore opens a new Chrome
// window with the saved urls, see docs/spikes/browser.md, registered before
// GenericAdapter so chrome.exe windows go here first
public sealed class BrowserAdapter : IAdapter
{
    private const string ChromeExe = "chrome.exe";
    private const string TitleSuffix = " - Google Chrome";
    // Capture runs on the UI thread, bound the wait so a missing extension does not
    // freeze it for long, a working extension answers in well under a second
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly Func<IReadOnlyList<BrowserWindow>> _queryWindows;
    private readonly IWindowWaiter _waiter;
    private readonly IWindowPlacer _placer;
    private readonly Action<string> _launch;
    private readonly Func<DateTime> _now;

    // A capture touches every Chrome window, so the pipe query is cached briefly and
    // shared across those windows rather than re-run per window
    private IReadOnlyList<BrowserWindow>? _cache;
    private string? _cacheError;
    private DateTime _cachedAt;

    public BrowserAdapter()
        : this(() => BrowserTabsClient.Query(QueryTimeout), new WindowWaiter(), new WindowPlacer(), LaunchChrome, () => DateTime.UtcNow) { }

    public BrowserAdapter(
        Func<IReadOnlyList<BrowserWindow>> queryWindows,
        IWindowWaiter waiter,
        IWindowPlacer placer,
        Action<string> launch,
        Func<DateTime> now)
    {
        _queryWindows = queryWindows ?? throw new ArgumentNullException(nameof(queryWindows));
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
        _now = now ?? throw new ArgumentNullException(nameof(now));
    }

    public string Id => "browser";

    public bool CanHandle(WindowInfo window) =>
        window.UnsupportedReason is null && IsChrome(window.ExePath);

    public Entry Capture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IReadOnlyList<BrowserWindow> windows = CachedQuery();

        IReadOnlyList<string>? urls = MatchUrls(window.Title, windows);
        if (urls is null)
        {
            throw new CaptureException(
                $"{window.Title}: couldn't match this Chrome window to its tabs (is more than one Chrome window on the same page?).");
        }

        return new BrowserEntry(DisplayNameFor(window.Title, urls), window.Placement, urls);
    }

    public void Restore(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry is not BrowserEntry browser)
        {
            throw new RestoreException($"{entry.DisplayName}: BrowserAdapter cannot restore a {entry.GetType().Name}.");
        }

        if (browser.Urls.Count == 0)
        {
            throw new RestoreException($"{browser.DisplayName}: no urls to open.");
        }

        try
        {
            _launch(BuildArguments(browser.Urls));
        }
        catch (Exception ex)
        {
            throw new RestoreException($"{browser.DisplayName}: failed to launch Chrome.", ex);
        }

        nint handle = _waiter.WaitForNewWindow(ChromeExe, browser.DisplayName);

        if (browser.Placement is not null)
        {
            _placer.Apply(handle, browser.Placement, targetElevated: false);
        }
    }

    // Matches an OS window title ("<active tab> - Google Chrome") to a reported
    // window by its active tab title, falls back to the only window when there is
    // one, returns null when the match is absent or ambiguous
    public static IReadOnlyList<string>? MatchUrls(string windowTitle, IReadOnlyList<BrowserWindow> windows)
    {
        if (windows.Count == 0)
        {
            return null;
        }

        string active = NormalizeTitle(StripSuffix(windowTitle));
        List<BrowserWindow> matches = windows
            .Where(w => string.Equals(NormalizeTitle(w.Title), active, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 1)
        {
            return matches[0].Urls;
        }

        if (matches.Count == 0 && windows.Count == 1)
        {
            return windows[0].Urls;
        }

        return null;
    }

    // chrome.exe --new-window "url1" "url2" ..., the first url opens the window and
    // the rest open as tabs in it
    public static string BuildArguments(IReadOnlyList<string> urls) =>
        "--new-window " + string.Join(" ", urls.Select(u => $"\"{u}\""));

    private IReadOnlyList<BrowserWindow> CachedQuery()
    {
        bool fresh = _now() - _cachedAt < CacheTtl && (_cache is not null || _cacheError is not null);
        if (!fresh)
        {
            try
            {
                _cache = _queryWindows();
                _cacheError = null;
            }
            catch (BrowserUnavailableException ex)
            {
                _cache = null;
                _cacheError = ex.Message;
            }

            _cachedAt = _now();
        }

        if (_cacheError is not null)
        {
            throw new CaptureException(_cacheError);
        }

        return _cache!;
    }

    private static string StripSuffix(string title) =>
        title.EndsWith(TitleSuffix, StringComparison.Ordinal)
            ? title[..^TitleSuffix.Length]
            : title;

    // The OS window title and the extension's tab title are sampled a moment apart
    // and a browser prepends an unread/notification count like "(3) " to the tab
    // title (YouTube does this), so drop a leading "(n) " count and trim before
    // comparing, otherwise the same window fails to match itself and, with a second
    // Chrome window open, the capture is reported as ambiguous
    private static string NormalizeTitle(string title)
    {
        string t = title.Trim();

        if (t.Length > 2 && t[0] == '(')
        {
            int j = 1;
            while (j < t.Length && char.IsDigit(t[j]))
            {
                j++;
            }

            if (j > 1 && j < t.Length && t[j] == ')')
            {
                j++;
                while (j < t.Length && t[j] == ' ')
                {
                    j++;
                }

                t = t[j..];
            }
        }

        return t;
    }

    private static bool IsChrome(string? exePath) =>
        exePath is not null &&
        string.Equals(Path.GetFileName(exePath), ChromeExe, StringComparison.OrdinalIgnoreCase);

    private static string DisplayNameFor(string windowTitle, IReadOnlyList<string> urls)
    {
        string label = StripSuffix(windowTitle);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Chrome";
        }

        return urls.Count <= 1 ? $"Chrome - {label}" : $"Chrome - {label} +{urls.Count - 1}";
    }

    private static void LaunchChrome(string arguments)
    {
        string chrome = ChromePath() ?? ChromeExe;
        Process.Start(new ProcessStartInfo(chrome, arguments) { UseShellExecute = false });
    }

    // Chrome's standard install locations, first that exists wins
    private static string? ChromePath()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
