using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Sessions.Core;

// Chrome native messaging framing, each message is a 4-byte little-endian length
// then that many bytes of UTF-8 JSON, the same framing is used on the named pipe
// between the host and the app, see docs/spikes/browser.md
public static class NativeMessaging
{
    // Chrome caps a single host-to-extension message at 1 MB, guard against a
    // bogus length prefix allocating wildly
    public const int MaxMessageBytes = 1024 * 1024;

    public static void WriteFrame(Stream destination, string json)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(json);

        byte[] data = Encoding.UTF8.GetBytes(json);
        Span<byte> prefix = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)data.Length);

        destination.Write(prefix);
        destination.Write(data);
        destination.Flush();
    }

    // Reads one frame, or null at a clean end of stream (the length prefix could
    // not be read in full), a partial body after a valid prefix is a broken stream
    public static string? ReadFrame(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] prefix = new byte[4];
        if (!TryReadExactly(source, prefix))
        {
            return null;
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (length == 0)
        {
            return string.Empty;
        }

        if (length > MaxMessageBytes)
        {
            throw new InvalidDataException($"Native message length {length} exceeds the {MaxMessageBytes} byte cap.");
        }

        byte[] data = new byte[length];
        if (!TryReadExactly(source, data))
        {
            throw new InvalidDataException("Native message ended before its declared length.");
        }

        return Encoding.UTF8.GetString(data);
    }

    // Reads buffer.Length bytes, looping because one read may return fewer, false
    // when the stream ends before the first byte of the buffer
    private static bool TryReadExactly(Stream source, byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = source.Read(buffer, read, buffer.Length - read);
            if (n == 0)
            {
                if (read == 0)
                {
                    return false; // clean end of stream at a frame boundary
                }

                throw new InvalidDataException("Native message stream ended mid-frame.");
            }

            read += n;
        }

        return true;
    }
}
