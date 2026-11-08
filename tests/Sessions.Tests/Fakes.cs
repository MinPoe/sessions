using Sessions.Core;

namespace Sessions.Tests;

// A minimal entry tied to a given adapter id, for exercising adapters/runners
internal sealed record FakeEntry(string DisplayName, string AdapterIdValue)
    : Entry(DisplayName, Placement: null)
{
    public override string AdapterId => AdapterIdValue;
}

// Configurable adapter: records whether it was asked to restore, and can fail
internal sealed class FakeAdapter : IAdapter
{
    private readonly bool _canHandle;
    private readonly string? _failWith;

    public FakeAdapter(string id, bool canHandle = true, string? failWith = null)
    {
        Id = id;
        _canHandle = canHandle;
        _failWith = failWith;
    }

    public string Id { get; }
    public int RestoreCount { get; private set; }

    public bool CanHandle(WindowInfo window) => _canHandle;

    public Entry Capture(WindowInfo window) => new FakeEntry(window.Title, Id);

    public void Restore(Entry entry)
    {
        RestoreCount++;
        if (_failWith is not null)
        {
            throw new RestoreException(_failWith);
        }
    }
}
