using System;
using System.Numerics;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Known-answer tests computed independently of the library. The expected strings were
/// produced with Python using plain integer arithmetic; the library uses byte-array
/// long division/multiplication, so the two implementations do not share a code path.
/// </summary>
public sealed class KnownAnswerTests
{
    [Theory]
    [InlineData(0UL, "0000000")]
    [InlineData(1UL, "0000001")]
    [InlineData(35UL, "000000z")]
    [InlineData(36UL, "0000010")]
    [InlineData(1295UL, "00000zz")]
    [InlineData(1296UL, "0000100")]
    [InlineData(46655UL, "0000zzz")]
    [InlineData(1679615UL, "000zzzz")]
    [InlineData(60466175UL, "00zzzzz")]
    [InlineData(2176782335UL, "0zzzzzz")]
    [InlineData(78364164095UL, "zzzzzzz")]
    public void TimestampFieldUsesFixedWidthLowercaseBase36(ulong timestamp, string expectedPrefix)
    {
        PikaidId id = TestIds.FromParts(timestamp, "000000000000000000000000");

        Assert.StartsWith(expectedPrefix, id.ToString(), StringComparison.Ordinal);
        Assert.Equal(expectedPrefix, id.ToString().Substring(0, PikaidId.TimestampLength));
        Assert.Equal(timestamp, id.Timestamp);
    }

    [Theory]
    [InlineData("000000000000000000000000", "0000000000000000000")]
    [InlineData("000000000000000000000001", "0000000000000000001")]
    [InlineData("000000000000000000000023", "000000000000000000z")]
    [InlineData("000000000000000000000024", "0000000000000000010")]
    [InlineData("00000000000000000000050f", "00000000000000000zz")]
    [InlineData("000000000000000000000510", "0000000000000000100")]
    [InlineData("2153e468b91c6d1000000000", "1000000000000000000")]
    [InlineData("ffffffffffffffffffffffff", "7oiylpimjg5u2ca1ypr")]
    public void RandomnessFieldUsesFixedWidthLowercaseBase36(string entropyHex, string expectedSuffix)
    {
        PikaidId id = TestIds.FromParts(0, entropyHex);

        Assert.EndsWith(expectedSuffix, id.ToString(), StringComparison.Ordinal);
        Assert.Equal(expectedSuffix, id.ToString().Substring(PikaidId.TimestampLength));
        Assert.Equal(entropyHex, id.RandomnessHex);
    }

    [Theory]
    [InlineData(
        0UL,
        "000000000000000000000000",
        "00000000000000000000000000",
        "0000000000000000000000000000000000")]
    [InlineData(
        1UL,
        "000000000000000000000001",
        "00000010000000000000000001",
        "0000000001000000000000000000000001")]
    [InlineData(
        1700000000UL,
        "00112233445566778899aabb",
        "0s44we8002loz3po6mcf6vsn23",
        "006553f10000112233445566778899aabb")]
    [InlineData(
        78364164095UL,
        "ffffffffffffffffffffffff",
        "zzzzzzz7oiylpimjg5u2ca1ypr",
        "123ede3fffffffffffffffffffffffffff")]
    [InlineData(
        46655UL,
        "2153e468b91c6d1000000000",
        "0000zzz1000000000000000000",
        "000000b63f2153e468b91c6d1000000000")]
    [InlineData(
        36UL,
        "2153e468b91c6d0fffffffff",
        "00000100zzzzzzzzzzzzzzzzzz",
        "00000000242153e468b91c6d0fffffffff")]
    public void KnownIdentifierEncodesAndDecodesBothForms(
        ulong timestamp,
        string entropyHex,
        string expectedId,
        string expectedBinHex)
    {
        PikaidId fromParts = TestIds.FromParts(timestamp, entropyHex);

        Assert.Equal(expectedId, fromParts.ToString());
        Assert.Equal(expectedBinHex, Hex.Format(fromParts.ToByteArray()));
        Assert.Equal(timestamp, fromParts.Timestamp);
        Assert.Equal(entropyHex, fromParts.RandomnessHex);

        PikaidId fromText = PikaidId.Parse(expectedId);
        PikaidId fromBinary = PikaidId.FromBytes(Hex.Parse(expectedBinHex));

        Assert.Equal(fromParts, fromText);
        Assert.Equal(fromParts, fromBinary);
        Assert.Equal(expectedId, fromBinary.ToString());
        Assert.Equal(expectedBinHex, Hex.Format(fromText.ToByteArray()));
    }

    [Fact]
    public void MaximumTimestampWithZeroEntropyIsCanonical()
    {
        PikaidId id = TestIds.FromParts(PikaidId.MaxTimestamp, "000000000000000000000000");

        Assert.Equal("zzzzzzz0000000000000000000", id.ToString());
        Assert.Equal("123ede3fff000000000000000000000000", Hex.Format(id.ToByteArray()));
        Assert.Equal(PikaidId.MaxTimestamp, id.Timestamp);
    }

    [Fact]
    public void MaximumTimestampBinaryPrefixIsFiveBytesBigEndian()
    {
        byte[] binary = Hex.Parse("123ede3fff000000000000000000000000");

        Assert.Equal(PikaidId.BinaryLength, binary.Length);
        Assert.Equal(new byte[] { 0x12, 0x3E, 0xDE, 0x3F, 0xFF }, binary[..5]);

        PikaidId id = PikaidId.FromBytes(binary);
        Assert.Equal(PikaidId.MaxTimestamp, id.Timestamp);
        Assert.Equal("zzzzzzz0000000000000000000", id.ToString());
    }

    [Fact]
    public void ThirtySixToTheEighteenFitsNineteenDigitsButTwoToTheNinetySixDoesNot()
    {
        BigInteger boundary = BigInteger.Pow(36, 18);
        BigInteger overflow = BigInteger.Pow(2, 96);

        byte[] boundaryBytes = boundary.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] overflowBytes = overflow.ToByteArray(isUnsigned: true, isBigEndian: true);

        Assert.Equal(12, boundaryBytes.Length);
        Assert.Equal(13, overflowBytes.Length);
        Assert.Equal("1000000000000000000", ReferenceBase36.EncodeEntropy(boundaryBytes));

        // Nineteen Base36 digits can hold up to 36^19 - 1, which is larger than 2^96.
        // The format therefore rejects by the 96-bit randomness limit, not by width.
        Assert.Equal("7oiylpimjg5u2ca1yps", ReferenceBase36.EncodeValue(overflow, PikaidId.RandomnessLength));
    }

    [Fact]
    public void FirstValueAboveTheNinetySixBitLimitIsRejectedByParse()
    {
        // 36^18 is the 19-digit positional boundary and still fits in 96 bits.
        string boundary = "0000000" + "1000000000000000000";

        // 2^96 is the smallest integer that needs more than 96 bits.
        string overflow = "0000000" + "7oiylpimjg5u2ca1yps";

        Assert.True(PikaidId.TryParse(boundary, out PikaidId parsed));
        Assert.Equal("2153e468b91c6d1000000000", parsed.RandomnessHex);
        Assert.Equal(0UL, parsed.Timestamp);

        Assert.False(PikaidId.TryParse(overflow, out _));
        Assert.False(PikaidId.IsValid(overflow));
        Assert.True(PikaidId.IsWellFormed(overflow));
    }
}
