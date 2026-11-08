using System.Text.Json.Serialization;

namespace Sessions.Core;

// A session entry: something to launch and, optionally, where to place it,
// serialized polymorphically on the "adapter" discriminator
[JsonPolymorphic(TypeDiscriminatorPropertyName = "adapter")]
[JsonDerivedType(typeof(GenericEntry), "generic")]
[JsonDerivedType(typeof(VsCodeEntry), "vscode")]
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
