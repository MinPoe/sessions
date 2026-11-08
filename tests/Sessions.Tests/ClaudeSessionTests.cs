using System;
using Sessions.Core;

namespace Sessions.Tests;

public class ClaudeSessionTests
{
    [Theory]
    [InlineData(@"C:\Users\ZEPHYRUS G14\Desktop\sessions", "C--Users-ZEPHYRUS-G14-Desktop-sessions")]
    [InlineData(@"C:\a.b\c", "C--a-b-c")]
    [InlineData(@"C:\Users\ZEPHYR~1\x", "C--Users-ZEPHYR-1-x")]
    public void EncodeCwd_ReplacesNonAlphanumericWithDash(string cwd, string expected)
    {
        Assert.Equal(expected, ClaudeSession.EncodeCwd(cwd));
    }

    [Fact]
    public void SelectNewestSessionId_ReturnsHighestModified()
    {
        var candidates = new[]
        {
            ("old", DateTimeOffset.UnixEpoch),
            ("newest", DateTimeOffset.UnixEpoch.AddHours(2)),
            ("middle", DateTimeOffset.UnixEpoch.AddHours(1)),
        };

        Assert.Equal("newest", ClaudeSession.SelectNewestSessionId(candidates));
    }

    [Fact]
    public void SelectNewestSessionId_NoCandidates_ReturnsNull()
    {
        Assert.Null(ClaudeSession.SelectNewestSessionId(Array.Empty<(string, DateTimeOffset)>()));
    }
}
