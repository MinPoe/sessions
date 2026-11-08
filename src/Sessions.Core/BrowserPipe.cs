namespace Sessions.Core;

// The named pipe the browser host and the Sessions app talk over, the app is the
// server, the Chrome-spawned host is the client, framed with NativeMessaging
public static class BrowserPipe
{
    // Full path is \\.\pipe\sessions.browser
    public const string Name = "sessions.browser";

    // The native messaging host registered with Chrome
    public const string HostName = "com.sessions.browser";
}
