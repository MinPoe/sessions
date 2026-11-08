using System.IO;
using System.IO.Pipes;
using System.Threading;
using Sessions.Core;

// BrowserHost, Chrome launches this as the native messaging host, it is a thin
// relay between Chrome's stdio frames and the Sessions app's named pipe, nothing
// but framed messages ever touches stdout, any diagnostics go to stderr, see
// docs/spikes/browser.md

// Binary stdio, Chrome speaks raw bytes, never Console text
using Stream stdin = Console.OpenStandardInput();
using Stream stdout = Console.OpenStandardOutput();

// The app is the pipe server but only while a capture is in flight, so the host
// waits for it rather than exiting when it is absent, this keeps the host process
// (and with it Chrome's native port, which keeps the extension's service worker
// alive) up across captures instead of flapping, exit only when Chrome closes the
// port, which shows up as end-of-stream on stdin while awaiting a reply
while (true)
{
    using var pipe = new NamedPipeClientStream(".", BrowserPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);

    try
    {
        pipe.Connect(); // waits indefinitely for the app to open its server
    }
    catch (IOException ex)
    {
        // A transient failure opening the pipe, pause briefly and try again rather
        // than spin, the app will be there on a later attempt
        Console.Error.WriteLine($"BrowserHost: pipe connect failed ({ex.Message}), retrying.");
        Thread.Sleep(500);
        continue;
    }

    try
    {
        // The app drives the exchange, one request (getWindows) and one reply per
        // connection, when it disposes its server ReadFrame(pipe) hits end-of-stream
        // and we loop back to wait for the next capture
        while (NativeMessaging.ReadFrame(pipe) is { } request)
        {
            NativeMessaging.WriteFrame(stdout, request); // -> Chrome -> extension

            string? reply = NativeMessaging.ReadFrame(stdin); // <- extension via Chrome
            if (reply is null)
            {
                return; // Chrome closed the port, the host is done
            }

            NativeMessaging.WriteFrame(pipe, reply); // -> app
        }
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
    {
        // The pipe broke or sent a bad frame, drop it and wait for a fresh capture
        Console.Error.WriteLine($"BrowserHost: pipe relay ended ({ex.Message}), reconnecting.");
    }
}
