using System.Collections.Generic;

namespace Sessions.Core;

// Outcome of restoring a single entry.
public sealed record RestoreResult(Entry Entry, bool Success, string? ErrorMessage);

// Restores a session's entries one at a time, continuing past failures and
// reporting the outcome of each.
public sealed class RestoreRunner
{
    private readonly AdapterRegistry _registry;

    public RestoreRunner(AdapterRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<RestoreResult> Run(IEnumerable<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<RestoreResult> results = new();
        foreach (Entry entry in entries)
        {
            IAdapter? adapter = _registry.SelectForRestore(entry);
            if (adapter is null)
            {
                results.Add(new RestoreResult(entry, false, $"No adapter for '{entry.AdapterId}'."));
                continue;
            }

            try
            {
                adapter.Restore(entry);
                results.Add(new RestoreResult(entry, true, null));
            }
            catch (RestoreException ex)
            {
                results.Add(new RestoreResult(entry, false, ex.Message));
            }
        }

        return results;
    }
}
