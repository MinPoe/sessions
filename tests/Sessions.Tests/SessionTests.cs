using System.Text.Json;
using Sessions.Core;

namespace Sessions.Tests;

public class SessionTests
{
    [Fact]
    public void Session_WithEntries_RoundTrips()
    {
        Session original = new(
            SchemaVersion: Session.CurrentSchemaVersion,
            Id: Guid.NewGuid(),
            Name: "Work",
            CreatedAt: DateTimeOffset.UtcNow,
            Entries: new Entry[]
            {
                new GenericEntry("Notepad", new Placement(10, 20, 300, 400, 1), @"C:\Windows\notepad.exe", ""),
                new GenericEntry("Calc", null, @"C:\Windows\System32\calc.exe", "--flag"),
            });

        string json = JsonSerializer.Serialize(original);
        Session? restored = JsonSerializer.Deserialize<Session>(json);

        Assert.Equal(original, restored);
    }
}
