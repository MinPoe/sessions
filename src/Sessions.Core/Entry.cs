using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sessions.Core;

// A session entry: something to launch and, optionally, where to place it,
// serialized polymorphically on the "adapter" discriminator
[JsonPolymorphic(TypeDiscriminatorPropertyName = "adapter")]
[JsonDerivedType(typeof(GenericEntry), "generic")]
[JsonDerivedType(typeof(VsCodeEntry), "vscode")]
[JsonDerivedType(typeof(TerminalEntry), "terminal")]
public abstract record Entry(string DisplayName, Placement? Placement)
{
    // The adapter that restores this entry; mirrors the JSON discriminator
    [JsonIgnore]
    public abstract string AdapterId { get; }
}

// Launches an arbitrary executable with optional command-line arguments
public sealed record GenericEntry(string DisplayName, Placement? Placement, string ExePath, string Arguments)
    : Entry(DisplayName, Placement)
{
    [JsonIgnore]
    public override string AdapterId => "generic";
}

// Reopens a VS Code folder or .code-workspace, Path is the folder or workspace file
public sealed record VsCodeEntry(string DisplayName, Placement? Placement, string Path)
    : Entry(DisplayName, Placement)
{
    [JsonIgnore]
    public override string AdapterId => "vscode";
}

// One tab inside a Windows Terminal window: a shell opened in Cwd, Command is a
// command to run in that shell (an ssh command line) or null, ClaudeSessionId is
// set when the tab was running Claude Code and names the session to resume
public sealed record TerminalTab(string Cwd, string ShellPath, string? Command, string? ClaudeSessionId);

// Reopens a Windows Terminal window with its tabs, one wt window holds every tab
public sealed record TerminalEntry(string DisplayName, Placement? Placement, IReadOnlyList<TerminalTab> Tabs)
    : Entry(DisplayName, Placement)
{
    [JsonIgnore]
    public override string AdapterId => "terminal";

    // Compare Tabs by sequence, the default record equality is by reference which
    // breaks round-trips (array in, List out), mirrors Session
    public bool Equals(TerminalEntry? other) =>
        other is not null &&
        DisplayName == other.DisplayName &&
        Placement == other.Placement &&
        Tabs.SequenceEqual(other.Tabs);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(DisplayName);
        hash.Add(Placement);
        foreach (TerminalTab tab in Tabs)
        {
            hash.Add(tab);
        }

        return hash.ToHashCode();
    }
}
