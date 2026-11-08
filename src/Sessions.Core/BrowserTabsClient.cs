using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sessions.Core;

// One Chrome window as reported by the extension, Title is the active tab title
// used to match this to an OS window, Urls are the http/https tabs in order
public sealed record BrowserWindow(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("focused")] bool Focused,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("urls")] IReadOnlyList<string> Urls);

// Asks the extension (through the native host) for the open Chrome windows and
// their tabs, the app is the pipe server, the Chrome-spawned host is the client,
// see docs/spikes/browser.md
public static class BrowserTabsClient
{
    private sealed record GetWindowsReply(
        [property: JsonPropertyName("windows")] IReadOnlyList<BrowserWindow> Windows);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Opens the pipe, waits for the host to connect (the extension's reconnect loop
    // spawns it), requests the windows, and returns the reply, throws on timeout or
    // a closed connection so the adapter can report the browser as uncapturable
    public static IReadOnlyList<BrowserWindow> Query(TimeSpan timeout)
    {
        using var server = new NamedPipeServerStream(
            BrowserPipe.Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        Task connect = server.WaitForConnectionAsync();
        if (!connect.Wait(timeout))
        {
            throw new BrowserUnavailableException(
                "No response from the Chrome extension, is it loaded and the browser host installed?");
        }

        NativeMessaging.WriteFrame(server, """{"cmd":"getWindows"}""");

        string? reply = NativeMessaging.ReadFrame(server);
        if (reply is null)
        {
            throw new BrowserUnavailableException("The browser host closed before replying.");
        }

        GetWindowsReply? parsed = JsonSerializer.Deserialize<GetWindowsReply>(reply, Options);
        return parsed?.Windows ?? Array.Empty<BrowserWindow>();
    }
}

// Thrown when the browser extension or host cannot be reached
public sealed class BrowserUnavailableException : Exception
{
    public BrowserUnavailableException(string message) : base(message) { }
}
