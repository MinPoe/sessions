using System.Collections.Generic;
using System.Linq;

namespace Sessions.Core;

// Holds the adapters in a fixed precedence: specific adapters first, the
// catch-all generic adapter last. Capture picks the first that CanHandle;
// restore resolves by the entry's adapter id.
public sealed class AdapterRegistry
{
    private readonly IReadOnlyList<IAdapter> _adapters;

    public AdapterRegistry(IEnumerable<IAdapter> specificAdapters, IAdapter generic)
    {
        ArgumentNullException.ThrowIfNull(specificAdapters);
        ArgumentNullException.ThrowIfNull(generic);

        List<IAdapter> ordered = specificAdapters.ToList();
        ordered.Add(generic); // generic is always last
        _adapters = ordered;
    }

    public IReadOnlyList<IAdapter> Adapters => _adapters;

    // First adapter that can capture this window, in precedence order.
    public IAdapter? SelectForCapture(WindowInfo window) =>
        _adapters.FirstOrDefault(a => a.CanHandle(window));

    // Adapter that restores this entry, matched by id.
    public IAdapter? SelectForRestore(Entry entry) =>
        _adapters.FirstOrDefault(a => a.Id == entry.AdapterId);
}
