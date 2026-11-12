using System.Buffers;
using Wire.Handlers.Commands;

namespace Wire.Tests;

// The frame id is a varint: 7 bits in each byte, low bits first. Small ids keep a 1-byte header.
public sealed class VarintTests
{
    [TestCase(0, new byte[] { 0x00 })]
    [TestCase(127, new byte[] { 0x7F })]
    [TestCase(128, new byte[] { 0x80, 0x01 })]
    [TestCase(16383, new byte[] { 0xFF, 0x7F })]
    [TestCase(16384, new byte[] { 0x80, 0x80, 0x01 })]
    [TestCase(int.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 })]
    public void Write_GivesTheBytes_ReadGivesTheValue(int value, byte[] bytes)
    {
        ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

        WireVarint.Write(value, output);

        Assert.That(output.WrittenSpan.ToArray(), Is.EqualTo(bytes));
        Assert.That(WireVarint.Read(bytes, out int length), Is.EqualTo(value));
        Assert.That(length, Is.EqualTo(bytes.Length));
    }

    [Test]
    public void Read_StopsAtTheLastByte()
    {
        byte[] frame = { 0x80, 0x01, 0xAA, 0xBB };

        Assert.That(WireVarint.Read(frame, out int length), Is.EqualTo(128));
        Assert.That(length, Is.EqualTo(2));
    }

    [Test]
    public void Read_Empty_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => WireVarint.Read(Array.Empty<byte>(), out _)));
    }

    [Test]
    public void Read_NoLastByte_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => WireVarint.Read(new byte[] { 0x80, 0x80 }, out _)));
    }

    [Test]
    public void Read_MoreThanFiveBytes_Throws()
    {
        byte[] frame = { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };

        Assert.Throws<InvalidOperationException>(new Action(() => WireVarint.Read(frame, out _)));
    }

    [Test]
    public void Write_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(new Action(() => WireVarint.Write(-1, new ArrayBufferWriter<byte>())));
    }
}
