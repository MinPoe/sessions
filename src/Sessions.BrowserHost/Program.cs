using System.IO;
using System.IO.Pipes;
using Sessions.Core;

// BrowserHost, Chrome launches this as the native messaging host, it is a thin
// relay between Chrome's stdio frames and the Sessions app's named pipe, nothing
// but framed messages ever touches stdout, any diagnostics go to stderr, see
// docs/spikes/browser.md

// Binary stdio, Chrome speaks raw bytes, never Console text
using Stream stdin = Console.OpenStandardInput();
using Stream stdout = Console.OpenStandardOutput();

using var pipe = new NamedPipeClientStream(".", BrowserPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);

try
{
    // The app may not be listening, give it a short window, if it never shows the
    // host exits and Chrome's port disconnects, the extension retries later
    pipe.Connect(3000);
}
catch (Exception ex) when (ex is TimeoutException or IOException)
{
    Console.Error.WriteLine($"BrowserHost: no Sessions pipe to connect to ({ex.Message}).");
    return;
}

// Pump Chrome -> pipe and pipe -> Chrome until either side closes
Task toPipe = Relay(stdin, pipe);
Task toChrome = Relay(pipe, stdout);
await Task.WhenAny(toPipe, toChrome);
return;

static Task Relay(Stream from, Stream to) => Task.Run(() =>
{
    try
    {
        while (NativeMessaging.ReadFrame(from) is { } message)
        {
            NativeMessaging.WriteFrame(to, message);
        }
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
    {
        // A closed stream or a broken frame ends this direction, let the other
        // pump finish and the process exit
    }
});
