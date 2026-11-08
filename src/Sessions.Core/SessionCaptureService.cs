using System.Collections.Generic;
using System.Linq;

namespace Sessions.Core;

// A window that couldn't be captured, with the reason
public sealed record CaptureFailure(WindowInfo Window, string Reason);

// The result of capturing a session: the built session plus any per-window failures
public sealed record SessionCaptureResult(Session Session, IReadOnlyList<CaptureFailure> Failures);

// Builds a Session from the user's selected windows, using the first matching
// adapter per window and collecting (rather than throwing on) per-window failures
public sealed class SessionCaptureService
{
    private readonly AdapterRegistry _registry;
    private readonly Func<IEnumerable<string>> _existingNames;

    public SessionCaptureService(AdapterRegistry registry, Func<IEnumerable<string>> existingNames)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _existingNames = existingNames ?? throw new ArgumentNullException(nameof(existingNames));
    }

    public SessionCaptureResult Capture(IEnumerable<WindowInfo> windows, string name)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ValidateName(name);

        (IReadOnlyList<Entry> entries, IReadOnlyList<CaptureFailure> failures) = CaptureEntries(windows);

        Session session = new(
            SchemaVersion: Session.CurrentSchemaVersion,
            Id: Guid.NewGuid(),
            Name: name,
            CreatedAt: DateTimeOffset.Now,
            Entries: entries);

        return new SessionCaptureResult(session, failures);
    }

    // Captures just the entries for a set of windows, used both to build a new
    // session and to add windows to an existing one, per-window failures are
    // collected rather than thrown
    public (IReadOnlyList<Entry> Entries, IReadOnlyList<CaptureFailure> Failures) CaptureEntries(
        IEnumerable<WindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        List<Entry> entries = new();
        List<CaptureFailure> failures = new();

        foreach (WindowInfo window in windows)
        {
            IAdapter? adapter = _registry.SelectForCapture(window);
            if (adapter is null)
            {
                failures.Add(new CaptureFailure(window, window.UnsupportedReason ?? "No adapter can capture this window."));
                continue;
            }

            try
            {
                entries.Add(adapter.Capture(window));
            }
            catch (CaptureException ex)
            {
                failures.Add(new CaptureFailure(window, ex.Message));
            }
        }

        return (entries, failures);
    }

    private void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Session name must not be empty.", nameof(name));
        }

        bool exists = _existingNames().Any(
            existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            throw new ArgumentException($"A session named '{name}' already exists.", nameof(name));
        }
    }
}
