using Sessions.Core;

namespace Sessions.Tests;

public class AdapterRegistryTests
{
    [Fact]
    public void Generic_IsAlwaysLast()
    {
        FakeAdapter specific = new("specific");
        FakeAdapter generic = new("generic");

        AdapterRegistry registry = new(new[] { specific }, generic);

        Assert.Equal(2, registry.Adapters.Count);
        Assert.Same(generic, registry.Adapters[^1]);
    }

    [Fact]
    public void SelectForCapture_PrefersSpecific_OverGeneric()
    {
        FakeAdapter specific = new("specific", canHandle: true);
        FakeAdapter generic = new("generic", canHandle: true);
        AdapterRegistry registry = new(new[] { specific }, generic);

        IAdapter? chosen = registry.SelectForCapture(SampleWindow());

        Assert.Same(specific, chosen);
    }

    [Fact]
    public void SelectForCapture_FallsBackToGeneric_WhenSpecificDeclines()
    {
        FakeAdapter specific = new("specific", canHandle: false);
        FakeAdapter generic = new("generic", canHandle: true);
        AdapterRegistry registry = new(new[] { specific }, generic);

        IAdapter? chosen = registry.SelectForCapture(SampleWindow());

        Assert.Same(generic, chosen);
    }

    [Fact]
    public void SelectForRestore_MatchesByAdapterId()
    {
        FakeAdapter specific = new("specific");
        FakeAdapter generic = new("generic");
        AdapterRegistry registry = new(new[] { specific }, generic);

        IAdapter? chosen = registry.SelectForRestore(new FakeEntry("w", "specific"));

        Assert.Same(specific, chosen);
    }

    private static WindowInfo SampleWindow() =>
        new(Handle: 1, Title: "w", ProcessId: 1) { ExePath = @"C:\app.exe" };
}
