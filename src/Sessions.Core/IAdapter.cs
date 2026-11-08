namespace Sessions.Core;

// An adapter knows how to capture a particular kind of window into an Entry and
// to restore that Entry later, Id links an adapter to the entries it produces
// (it matches the entry's "adapter" discriminator)
public interface IAdapter
{
    // Stable key, equal to the discriminator of the entries this adapter emits
    string Id { get; }

    // True when this adapter can capture the given window
    bool CanHandle(WindowInfo window);

    // Captures the window into an entry, or throws CaptureException
    Entry Capture(WindowInfo window);

    // Relaunches/repositions from an entry, or throws RestoreException
    void Restore(Entry entry);
}

// Thrown when a window cannot be captured
public sealed class CaptureException : Exception
{
    public CaptureException(string message) : base(message) { }
    public CaptureException(string message, Exception inner) : base(message, inner) { }
}

// Thrown when an entry cannot be restored
public sealed class RestoreException : Exception
{
    public RestoreException(string message) : base(message) { }
    public RestoreException(string message, Exception inner) : base(message, inner) { }
}
