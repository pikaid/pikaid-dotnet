using System;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Lossless conversions between the textual and binary forms, including leading zeros
/// and both boundaries.
/// </summary>
public sealed class ConversionTests
{
    [Theory]
    [InlineData("00000000000000000000000000")]
    [InlineData("00000010000000000000000001")]
    [InlineData("0s44we8002loz3po6mcf6vsn23")]
    [InlineData("zzzzzzz7oiylpimjg5u2ca1ypr")]
    [InlineData("00000100zzzzzzzzzzzzzzzzzz")]
    public void TextAndBinaryRoundTripLosslessly(string text)
    {
        PikaidId id = PikaidId.Parse(text);

        byte[] binary = id.ToByteArray();
        PikaidId fromBinary = PikaidId.FromBytes(binary);

        Assert.Equal(id, fromBinary);
        Assert.Equal(text, fromBinary.ToString());
        Assert.Equal(text, id.Value);
        Assert.Equal(Hex.Format(binary), Hex.Format(fromBinary.ToByteArray()));
    }

    [Fact]
    public void LeadingZerosArePreservedInBothDirections()
    {
        PikaidId id = TestIds.FromParts(1, "000000000000000000000001");

        Assert.Equal("00000010000000000000000001", id.ToString());
        Assert.Equal("0000000001000000000000000000000001", Hex.Format(id.ToByteArray()));
        Assert.Equal("000000000000000000000001", id.RandomnessHex);
        Assert.Equal(1UL, id.Timestamp);
    }

    [Fact]
    public void ToByteArrayReturnsAnIndependentArray()
    {
        PikaidId id = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        byte[] first = id.ToByteArray();
        byte[] expected = id.ToByteArray();

        first[0] = 0xFF;
        first[16] = 0xFF;

        Assert.Equal(expected, id.ToByteArray());
        Assert.NotSame(first, id.ToByteArray());
    }

    [Fact]
    public void ToRandomnessReturnsTheTwelveEntropyBytes()
    {
        PikaidId id = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        byte[] randomness = id.ToRandomness();

        Assert.Equal(PikaidId.RandomnessByteLength, randomness.Length);
        Assert.Equal("00112233445566778899aabb", Hex.Format(randomness));
        Assert.Equal(Hex.Format(randomness), id.RandomnessHex);
        Assert.NotSame(randomness, id.ToRandomness());
    }

    [Fact]
    public void WriteBytesWritesTheFiveByteBigEndianTimestampAndEntropy()
    {
        PikaidId id = TestIds.FromParts(0x0102030405UL, "0a0b0c0d0e0f101112131415");
        byte[] destination = new byte[PikaidId.BinaryLength];

        id.WriteBytes(destination);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destination[..5]);
        Assert.Equal(Hex.Parse("0a0b0c0d0e0f101112131415"), destination[5..]);
        Assert.Equal(destination, id.ToByteArray());
    }

    [Fact]
    public void WriteRandomnessWritesTheEntropyInOriginalOrder()
    {
        PikaidId id = TestIds.FromParts(0, "00112233445566778899aabb");
        byte[] destination = new byte[PikaidId.RandomnessByteLength];

        id.WriteRandomness(destination);

        Assert.Equal(Hex.Parse("00112233445566778899aabb"), destination);
    }

    [Fact]
    public void BinaryTimestampIsUnsignedBigEndian()
    {
        PikaidId maximum = TestIds.FromParts(PikaidId.MaxTimestamp, "000000000000000000000000");
        byte[] binary = maximum.ToByteArray();

        Assert.Equal(new byte[] { 0x12, 0x3E, 0xDE, 0x3F, 0xFF }, binary[..5]);
        Assert.Equal(PikaidId.MaxTimestamp, maximum.Timestamp);

        // 0xFFFFFFFFFF exceeds the timestamp maximum; the five-byte decoder must reject it
        // rather than wrap or truncate.
        byte[] overflow = TestIds.Binary(0xFFFFFFFFFFUL, Hex.Parse("000000000000000000000000"));
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, overflow[..5]);
        Assert.Throws<PikaidFormatException>(() => PikaidId.FromBytes(overflow));
    }

    [Fact]
    public void TimestampUtcIsUtcWithOneSecondResolution()
    {
        PikaidId epoch = PikaidId.Parse("00000000000000000000000000");

        Assert.Equal(DateTimeOffset.UnixEpoch, epoch.TimestampUtc);
        Assert.Equal(TimeSpan.Zero, epoch.TimestampUtc.Offset);
        Assert.Equal(0, epoch.TimestampUtc.Ticks % TimeSpan.TicksPerSecond);

        PikaidId sample = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        Assert.Equal(new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero), sample.TimestampUtc);
    }

    [Fact]
    public void TimestampUtcCoversTheWholeSupportedRange()
    {
        PikaidId maximum = TestIds.FromParts(PikaidId.MaxTimestamp, "000000000000000000000000");

        Assert.Equal(new DateTimeOffset(4453, 4, 5, 15, 21, 35, TimeSpan.Zero), maximum.TimestampUtc);
        Assert.Equal(PikaidId.MaxTimestamp, (ulong)maximum.TimestampUtc.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(0UL, "000000000000000000000000")]
    [InlineData(1UL, "000000000000000000000001")]
    [InlineData(1295UL, "00000000000000000000050f")]
    [InlineData(1296UL, "000000000000000000000510")]
    [InlineData(2176782335UL, "00000000000000000fffffff")]
    [InlineData(78364164095UL, "ffffffffffffffffffffffff")]
    public void TextBinaryRoundTripHoldsAcrossFieldBoundaries(ulong timestamp, string entropyHex)
    {
        PikaidId id = TestIds.FromParts(timestamp, entropyHex);

        Assert.Equal(id, PikaidId.FromBytes(id.ToByteArray()));
        Assert.Equal(id, PikaidId.Parse(id.ToString()));

        byte[] binary = Hex.Parse(Hex.Format(id.ToByteArray()));
        PikaidId decoded = PikaidId.FromBytes(binary);
        Assert.Equal(timestamp, decoded.Timestamp);
        Assert.Equal(entropyHex, decoded.RandomnessHex);
    }

    [Fact]
    public void DefensiveCopyIsNotSharedWithTheStruct()
    {
        PikaidId original = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        Span<byte> destination = stackalloc byte[PikaidId.BinaryLength];

        original.WriteBytes(destination);
        destination[0] = 0xFF;

        Assert.NotEqual((byte)0xFF, original.ToByteArray()[0]);
    }

    [Fact]
    public void MutatingAnyRandomnessOrBinaryCopyDoesNotChangeTheStruct()
    {
        PikaidId id = TestIds.FromParts(0x0102030405UL, "00112233445566778899aabb");

        byte[] randomness = id.ToRandomness();
        randomness[0] = 0xFF;
        randomness[PikaidId.RandomnessByteLength - 1] = 0xFF;

        Assert.Equal("00112233445566778899aabb", id.RandomnessHex);
        Assert.Equal("00112233445566778899aabb", Hex.Format(id.ToRandomness()));

        Span<byte> randomnessDestination = stackalloc byte[PikaidId.RandomnessByteLength];
        id.WriteRandomness(randomnessDestination);
        randomnessDestination[0] = 0xFF;
        randomnessDestination[PikaidId.RandomnessByteLength - 1] = 0xFF;

        Assert.Equal("00112233445566778899aabb", id.RandomnessHex);

        Span<byte> binaryDestination = stackalloc byte[PikaidId.BinaryLength];
        id.WriteBytes(binaryDestination);
        binaryDestination[PikaidId.TimestampByteLength] = 0xFF;
        binaryDestination[PikaidId.BinaryLength - 1] = 0xFF;

        Assert.Equal("00112233445566778899aabb", id.RandomnessHex);
        Assert.Equal("010203040500112233445566778899aabb", Hex.Format(id.ToByteArray()));
    }
}
