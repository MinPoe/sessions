# Spike: Chrome native messaging

What it takes to read the open tabs of each Chrome window, and to reopen them, findings for Google
Chrome on this machine (Chrome 157, `C:\Program Files\Google\Chrome\Application\chrome.exe`)

Chrome does not expose its open tabs to another process, only an extension can read `chrome.tabs`,
and an extension can only talk to a local program through native messaging, so the shape is:

```
Sessions app  <--named pipe-->  native host (spawned by Chrome)  <--stdio frames-->  Chrome  <-->  our MV3 extension
```

the native host is a thin relay, Chrome owns its lifetime, our app owns the pipe

## Host registration (Windows)

A native messaging host is registered per user with a registry key whose default value is the full
path to a manifest JSON file:

- key, `HKCU\Software\Google\Chrome\NativeMessagingHosts\<host.name>`
- default value, the absolute path to `manifest.json`

Verified on this machine, the key path is correct (Chrome already has
`com.microsoft.browsercore` registered there), and we can create, read back, and delete our own
`HKCU\...\NativeMessagingHosts\com.sessions.browser` key, so no admin rights are needed, HKCU is enough

Manifest shape:

```json
{
  "name": "com.sessions.browser",
  "description": "Sessions browser relay",
  "path": "C:\\full\\path\\to\\BrowserHost.exe",
  "type": "stdio",
  "allowed_origins": [ "chrome-extension://<EXTENSION_ID>/" ]
}
```

- `path` is the host executable, Chrome launches it as a child process of `chrome.exe`
- `type` is always `stdio`
- `allowed_origins` must list the exact extension id with the trailing slash, a wrong id means Chrome
  refuses to launch the host with "forbidden" and the extension sees a disconnect

## The extension id gotcha (pin it)

For an unpacked extension with no `key` in its manifest, Chrome derives the 32-character id from the
SHA-256 of the extension's absolute folder path (bytes mapped to the letters a-p), so the id CHANGES
if the folder moves, which would break `allowed_origins`,

Fix, pin the id by putting a `key` (base64 of a DER public key) in the extension `manifest.json`, then
the id is derived from the key and is stable wherever the folder sits, the install step computes the
same id from that key and writes it into `allowed_origins`,

id derivation, first 16 bytes of SHA-256(DER public key), each 4-bit nibble mapped 0-15 to a-p

This is the single most error-prone part, confirm the id in `chrome://extensions` matches the one in
`allowed_origins` after install

## Message framing

Each message both ways is a 4-byte little-endian unsigned length, then that many bytes of UTF-8 JSON,
verified by round-trip, `{"cmd":"getWindows"}` frames as `14 00 00 00` then the 20 JSON bytes,

Hard rules for the host:
- stdout carries ONLY framed messages, any stray `print`/log byte on stdout corrupts the stream and
  Chrome kills the host, all logging must go to stderr or a file
- read exactly the length prefix then exactly that many bytes, do not assume one read returns a whole
  message
- Chrome caps a single host-to-extension message at 1 MB, tab lists are tiny so this is not a concern
- when Chrome closes the port it closes the host's stdin (EOF), the host should exit

## Capture flow

- the Sessions app is the named-pipe server (e.g. `\\.\pipe\sessions.browser`), it opens the server
  only while a capture is in flight, not for the app's whole lifetime
- the extension service worker opens a long-lived port with `chrome.runtime.connectNative("com.sessions.browser")`, Chrome spawns the host, the host connects to the app's pipe as a client
- the host waits for that server rather than exiting when it is absent, `pipe.Connect()` blocks until
  the app opens its pipe for the next capture, so the host process (and the native port that keeps the
  service worker alive) stays up across captures instead of flapping, exit only when Chrome closes the
  port, seen as end-of-stream on stdin while awaiting a reply
- to capture, the app writes `{"cmd":"getWindows"}` down the pipe, the host frames it to Chrome, the
  service worker answers with `chrome.windows.getAll({populate:true})` mapped to `[{ id, urls:[...] }]`,
  the host relays the reply back down the pipe, one request and one reply per connection, the app then
  disposes its server and the host loops back to wait for the next capture
- windows are keyed by Chrome's window id so each Chrome window becomes one BrowserEntry

MV3 caveat, a service worker is not persistent, but an open native messaging port keeps it alive while
connected, so the worker should hold the port open rather than reconnect per request, confirm the
worker survives idle with the port open (manual)

## Restore

No extension or host needed to restore, launch a new window with the saved urls as arguments:

```
chrome.exe --new-window https://a.example https://b.example https://c.example
```

the first url opens the window, the rest open as tabs in it, each BrowserEntry restores to one window

## Verified here vs still manual

Verified on this machine:
- Chrome 157 present at the Program Files path
- the HKCU native-messaging-host key can be created, read, and deleted without admin
- the 4-byte length + UTF-8 JSON framing round-trips

Still to confirm manually (needs the unpacked extension loaded in Chrome, which this environment
cannot do):
- Chrome actually spawns the host from the manifest and the `allowed_origins` id matches
- a full round-trip, app pipe to host to extension and back, returns the per-window tab urls
- the service worker stays alive with the native port held open
