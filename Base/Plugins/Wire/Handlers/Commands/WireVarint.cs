using System.Buffers;

namespace Wire.Handlers.Commands;

// The frame id: 7 bits in each byte, low bits first. A high bit of 1 means that one more byte follows.
public static class WireVarint
{
    public const int MaxBytes = 5;

    public static void Write(int value, IBufferWriter<byte> output)
    {
        throw new NotImplementedException();
    }

    public static int Read(ReadOnlySpan<byte> frame, out int length)
    {
        throw new NotImplementedException();
    }
}
