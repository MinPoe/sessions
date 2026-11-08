namespace Sessions.Core;

// A top-level window the user could reasonably place, with what we could learn
// about its owning process. ExePath/CommandLine are null when they couldn't be
// read; UnsupportedReason explains why rather than guessing a value.
public sealed record WindowInfo(nint Handle, string Title, uint ProcessId)
{
    // Full path to the owning process's executable, or null if unreadable.
    public string? ExePath { get; init; }

    // Why this window can't be captured (e.g. exe path unreadable). Null = fine.
    public string? UnsupportedReason { get; init; }

    // The process command line, or null if empty/unreadable (adapters decide
    // whether a missing command line is an error).
    public string? CommandLine { get; init; }

    // The parent process id, or null if it couldn't be read.
    public uint? ParentProcessId { get; init; }
}
