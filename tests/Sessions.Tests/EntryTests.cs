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

    [Fact]
    public void VsCodeEntry_RoundTrips_ThroughBaseType()
    {
        Entry original = new VsCodeEntry(
            DisplayName: "sessions",
            Placement: new Placement(10, 20, 1200, 800, 1),
            Path: @"c:\Users\me\Desktop\sessions");

        string json = JsonSerializer.Serialize(original);
        Entry? restored = JsonSerializer.Deserialize<Entry>(json);

        Assert.Equal(original, restored);
        Assert.IsType<VsCodeEntry>(restored);
    }

    [Fact]
    public void TerminalEntry_RoundTrips_ThroughBaseType()
    {
        Entry original = new TerminalEntry(
            DisplayName: "Terminal - sessions +1",
            Placement: new Placement(0, 0, 1000, 700, 1),
            Tabs: new[]
            {
                new TerminalTab(@"C:\Users\me\Desktop\sessions", @"C:\pwsh.exe", Command: null, ClaudeSessionId: "abc-123"),
                new TerminalTab(@"C:\Users\me\box", @"C:\cmd.exe", Command: "ssh user@host", ClaudeSessionId: null),
            });

        string json = JsonSerializer.Serialize(original);
        Entry? restored = JsonSerializer.Deserialize<Entry>(json);

        Assert.Equal(original, restored);
        Assert.IsType<TerminalEntry>(restored);
    }
}
