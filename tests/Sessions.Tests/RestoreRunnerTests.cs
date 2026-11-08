using Sessions.Core;

namespace Sessions.Tests;

public class RestoreRunnerTests
{
    [Fact]
    public void Run_ContinuesPastFailure_AndReportsEach()
    {
        FakeAdapter ok = new("ok");
        FakeAdapter bad = new("bad", failWith: "boom");
        AdapterRegistry registry = new(new IAdapter[] { ok, bad }, new FakeAdapter("generic"));
        RestoreRunner runner = new(registry);

        Entry[] entries =
        {
            new FakeEntry("first", "ok"),
            new FakeEntry("second", "bad"),
            new FakeEntry("third", "ok"),
        };

        var results = runner.Run(entries);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("boom", results[1].ErrorMessage);
        Assert.True(results[2].Success);

        // The failure didn't stop the third entry from being restored.
        Assert.Equal(2, ok.RestoreCount);
        Assert.Equal(1, bad.RestoreCount);
    }

    [Fact]
    public void Run_UnknownAdapterId_IsReportedAsFailure()
    {
        AdapterRegistry registry = new(Array.Empty<IAdapter>(), new FakeAdapter("generic"));
        RestoreRunner runner = new(registry);

        var results = runner.Run(new[] { new FakeEntry("x", "missing") });

        RestoreResult only = Assert.Single(results);
        Assert.False(only.Success);
        Assert.Contains("missing", only.ErrorMessage);
    }
}
