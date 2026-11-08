using System.Collections.Generic;
using System.Linq;

namespace Sessions.Core;

// A named, persisted collection of entries to launch together
public sealed record Session(
    int SchemaVersion,
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Entry> Entries)
{
    // Bump when the on-disk shape changes; Load rejects anything it doesn't know
    public const int CurrentSchemaVersion = 1;

    // Compare Entries by sequence; the default record equality is by reference,
    // which breaks round-trips (Entry[] in, List<Entry> out)
    public bool Equals(Session? other) =>
        other is not null &&
        SchemaVersion == other.SchemaVersion &&
        Id == other.Id &&
        Name == other.Name &&
        CreatedAt == other.CreatedAt &&
        Entries.SequenceEqual(other.Entries);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(SchemaVersion);
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(CreatedAt);
        foreach (Entry entry in Entries)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}
