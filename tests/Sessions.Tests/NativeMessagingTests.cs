using System.IO;
using Sessions.Core;

namespace Sessions.Tests;

public class NativeMessagingTests
{
    [Fact]
    public void WriteFrame_ThenReadFrame_RoundTrips()
    {
        using MemoryStream stream = new();
        NativeMessaging.WriteFrame(stream, """{"cmd":"getWindows"}""");

        stream.Position = 0;
        Assert.Equal("""{"cmd":"getWindows"}""", NativeMessaging.ReadFrame(stream));
    }

    [Fact]
    public void WriteFrame_UsesFourByteLittleEndianLengthPrefix()
    {
        using MemoryStream stream = new();
        NativeMessaging.WriteFrame(stream, "abc"); // 3 UTF-8 bytes

        byte[] bytes = stream.ToArray();
        Assert.Equal(new byte[] { 0x03, 0x00, 0x00, 0x00 }, bytes[..4]);
        Assert.Equal("abc"u8.ToArray(), bytes[4..]);
    }

    [Fact]
    public void ReadFrame_MultipleMessages_ReadInOrder()
    {
        using MemoryStream stream = new();
        NativeMessaging.WriteFrame(stream, "one");
        NativeMessaging.WriteFrame(stream, "two");

        stream.Position = 0;
        Assert.Equal("one", NativeMessaging.ReadFrame(stream));
        Assert.Equal("two", NativeMessaging.ReadFrame(stream));
        Assert.Null(NativeMessaging.ReadFrame(stream)); // clean end of stream
    }

    [Fact]
    public void ReadFrame_HandlesChunkedReads()
    {
        using MemoryStream backing = new();
        NativeMessaging.WriteFrame(backing, """{"windows":[]}""");

        // A stream that hands back one byte at a time, like a pipe under load
        using OneByteStream drip = new(backing.ToArray());
        Assert.Equal("""{"windows":[]}""", NativeMessaging.ReadFrame(drip));
    }

    [Fact]
    public void ReadFrame_EmptyStream_ReturnsNull()
    {
        using MemoryStream stream = new();
        Assert.Null(NativeMessaging.ReadFrame(stream));
    }

    [Fact]
    public void ReadFrame_TruncatedBody_Throws()
    {
        // Prefix says 10 bytes, only 2 follow
        byte[] bad = { 0x0A, 0x00, 0x00, 0x00, (byte)'h', (byte)'i' };
        using MemoryStream stream = new(bad);
        Assert.Throws<InvalidDataException>(() => NativeMessaging.ReadFrame(stream));
    }

    // Returns a single byte per Read to exercise the read-exactly loop
    private sealed class OneByteStream(byte[] data) : Stream
    {
        private readonly byte[] _data = data;
        private int _pos;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _data.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = _data[_pos++];
            return 1;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
