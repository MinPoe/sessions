namespace Sessions.Core;

// A top-level window the user could reasonably place: handle, title, owning process.
public sealed record WindowInfo(nint Handle, string Title, uint ProcessId);
