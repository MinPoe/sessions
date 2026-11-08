using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Sessions.Core;

namespace Sessions.Tests;

public class BrowserTabsClientTests
{
    // The regression this guards: before the exchange was bounded, a host that
    // connected to the pipe but never replied left Query blocked on the read
    // forever, freezing the app on save. Query must give up within the timeout
    // whether nothing connects at all or a silent host connects. Both live in one
    // test so the two servers never race on the single global pipe name.
    // A unique pipe name per run so the test never binds the production pipe a real
    // browser host on this machine may be connected to
    private static readonly string PipeName = "sessions.browser.test." + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Query_UnresponsiveHost_GivesUpWithinTimeout()
    {
        // No host connects at all: the connect wait must expire and throw
        AssertThrowsWithin(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(2));

        // A host that connects but never answers: the read must expire and throw,
        // which is the exact hang the fix addresses
        using var stop = new CancellationTokenSource();
        Task client = Task.Run(async () =>
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, stop.Token);
                await Task.Delay(Timeout.Infinite, stop.Token); // connected, never replies
            }
            catch (OperationCanceledException) { } // torn down by the test, not a failure
        });

        try
        {
            AssertThrowsWithin(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(3));
        }
        finally
        {
            stop.Cancel();
            await client.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    private static void AssertThrowsWithin(TimeSpan timeout, TimeSpan budget)
    {
        var sw = Stopwatch.StartNew();
        Assert.Throws<BrowserUnavailableException>(() => BrowserTabsClient.Query(timeout, PipeName));
        sw.Stop();

        // The old code never returned here; the whole exchange is now bounded
        Assert.True(sw.Elapsed < budget, $"Query took {sw.Elapsed}, expected to give up near {timeout}.");
    }
}
