using System.Buffers;

namespace Wire.Handlers.Commands;

// The frame id: 7 bits in each byte, low bits first. A high bit of 1 means that one more byte follows.
public static class WireVarint
{
    public const int MaxBytes = 5;

    public static void Write(int value, IBufferWriter<byte> output)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A frame id is 0 or more.");
        }

        Span<byte> span = output.GetSpan(MaxBytes);
        uint rest = (uint)value;
        int count = 0;
        while (rest >= 0x80)
        {
            span[count++] = (byte)(rest | 0x80);
            rest >>= 7;
        }

        span[count++] = (byte)rest;
        output.Advance(count);
    }

    public static int Read(ReadOnlySpan<byte> frame, out int length)
    {
        if (frame.IsEmpty)
        {
            throw new InvalidOperationException("The frame is empty.");
        }

        ulong value = 0;
        for (int i = 0; i < MaxBytes && i < frame.Length; i++)
        {
            byte part = frame[i];
            value |= (ulong)(part & 0x7F) << (7 * i);
            if ((part & 0x80) == 0)
            {
                if (value > int.MaxValue)
                {
                    break;
                }

                length = i + 1;
                return (int)value;
            }
        }

        throw new InvalidOperationException(
            $"The frame id is not a varint of {MaxBytes} bytes or less with a value of int.MaxValue or less.");
    }
}
