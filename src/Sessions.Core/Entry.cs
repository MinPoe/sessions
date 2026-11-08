using System.Text.Json.Serialization;

namespace Sessions.Core;

// A session entry: something to launch and, optionally, where to place it.
// Serialized polymorphically on the "adapter" discriminator.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "adapter")]
[JsonDerivedType(typeof(GenericEntry), "generic")]
public abstract record Entry(string DisplayName, Placement? Placement);

// Launches an arbitrary executable with optional command-line arguments.
public sealed record GenericEntry(string DisplayName, Placement? Placement, string ExePath, string Arguments)
    : Entry(DisplayName, Placement);
