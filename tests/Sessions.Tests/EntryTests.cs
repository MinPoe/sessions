using System.Text.Json;
using Sessions.Core;

namespace Sessions.Tests;

public class EntryTests
{
    [Fact]
    public void GenericEntry_RoundTrips_ThroughBaseType()
    {
        Entry original = new GenericEntry(
            DisplayName: "Notepad",
            Placement: new Placement(100, 200, 800, 600, 1),
            ExePath: @"C:\Windows\notepad.exe",
            Arguments: "--example \"quoted arg\"");

        string json = JsonSerializer.Serialize(original);
        Entry? restored = JsonSerializer.Deserialize<Entry>(json);

        Assert.Equal(original, restored);
        Assert.IsType<GenericEntry>(restored);
    }
}
