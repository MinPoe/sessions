using System.Text.Json.Serialization;

namespace Sessions.Core;

// A session entry: something to launch and, optionally, where to place it,
// serialized polymorphically on the "adapter" discriminator
[JsonPolymorphic(TypeDiscriminatorPropertyName = "adapter")]
[JsonDerivedType(typeof(GenericEntry), "generic")]
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
