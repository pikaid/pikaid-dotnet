using System;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Strict decoding and validation behavior: every noncanonical representation is
/// rejected, nothing is normalized, truncated or coerced.
/// </summary>
public sealed class ValidationTests
{
    [Fact]
    public void NullStringIsNotValid()
    {
        Assert.False(PikaidId.IsValid(null));
        Assert.False(PikaidId.IsWellFormed((string?)null));
        Assert.False(PikaidId.TryParse((string?)null, out PikaidId result));
        Assert.Equal(default, result);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => PikaidId.Parse((string)null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0000000000000000000000000")]
    [InlineData("000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000")]
    public void StringsOfTheWrongLengthAreRejected(string value)
    {
        Assert.False(PikaidId.IsValid(value));
        Assert.False(PikaidId.IsWellFormed(value));
        Assert.False(PikaidId.TryParse(value, out _));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(value));
    }

    [Theory]
    [InlineData("000000A0000000000000000000")]
    [InlineData("000000a000000000000000000Z")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("0S44WE8002LOZ3PO6MCF6VSN23")]
    public void UppercaseCharactersAreRejected(string value)
    {
        Assert.False(PikaidId.IsValid(value));
        Assert.False(PikaidId.IsWellFormed(value));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(value));
    }

    [Theory]
    [InlineData("000000-0000000000000000000")]
    [InlineData("000000_0000000000000000000")]
    [InlineData("000000 0000000000000000000")]
    [InlineData(" 0000000000000000000000000")]
    [InlineData("0000000000000000000000000 ")]
    [InlineData("0000000000000000000000000\n")]
    [InlineData("0000000\t000000000000000000")]
    [InlineData("000000+0000000000000000000")]
    [InlineData("000000/0000000000000000000")]
    [InlineData("000000.0000000000000000000")]
    public void NonBase36CharactersAreRejected(string value)
    {
        Assert.Equal(PikaidId.TextLength, value.Length);
        Assert.False(PikaidId.IsValid(value));
        Assert.False(PikaidId.IsWellFormed(value));
        Assert.False(PikaidId.TryParse(value, out _));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(value));
    }

    [Theory]
    [InlineData("é")] // U+00E9, outside ASCII
    [InlineData("Ａ")] // U+FF21 fullwidth A
    [InlineData("٠")] // U+0660 Arabic-Indic digit zero
    [InlineData("\uD83D\uDE00")] // surrogate pair (two chars)
    [InlineData("\u0000")] // embedded NUL
    public void NonAsciiLookalikesAreRejected(string lookalike)
    {
        string value = "000000" + lookalike
            + new string('0', PikaidId.TextLength - 6 - lookalike.Length);

        Assert.Equal(PikaidId.TextLength, value.Length);
        Assert.False(PikaidId.IsWellFormed(value));
        Assert.False(PikaidId.IsValid(value));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(value));
    }

    [Theory]
    [InlineData("0000000zzzzzzzzzzzzzzzzzzz")] // all randomness digits set
    [InlineData("00000007oiylpimjg5u2ca1yps")] // exactly 2^96
    [InlineData("zzzzzzz7oiylpimjg5u2ca1yps")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void RandomnessValuesAboveNinetySixBitsAreRejected(string value)
    {
        Assert.True(PikaidId.IsWellFormed(value));
        Assert.False(PikaidId.IsValid(value));
        Assert.False(PikaidId.TryParse(value, out PikaidId result));
        Assert.Equal(default, result);
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(value));
    }

    [Theory]
    [InlineData("00000000000000000000000000", true)]
    [InlineData("00000000000000000000000001", true)]
    [InlineData("zzzzzzz7oiylpimjg5u2ca1ypr", true)]
    [InlineData("0000000zzzzzzzzzzzzzzzzzzz", false)] // well formed, randomness exceeds 96 bits
    public void BoundaryStringsReportTheirExactValidity(string value, bool expectedValid)
    {
        Assert.Equal(expectedValid, PikaidId.IsValid(value));
        Assert.True(PikaidId.IsWellFormed(value));
    }

    [Fact]
    public void WellFormedSpanRequiresExactLengthAndAlphabet()
    {
        Assert.True(PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzzz".AsSpan()));
        Assert.False(PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzz".AsSpan()));
        Assert.False(PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzzz0".AsSpan()));
        Assert.False(PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzzZ".AsSpan()));
        Assert.False(PikaidId.IsWellFormed(ReadOnlySpan<char>.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(34)]
    public void BinaryValuesOfTheWrongLengthAreRejected(int length)
    {
        byte[] binary = new byte[length];

        Assert.False(PikaidId.TryFromBytes(binary, out PikaidId result));
        Assert.Equal(default, result);
        Assert.Throws<PikaidFormatException>(() => PikaidId.FromBytes(binary));
    }

    [Theory]
    [InlineData("123ede4000000000000000000000000000")] // MaxTimestamp + 1
    [InlineData("ffffffffff000000000000000000000000")] // 2^40 - 1
    [InlineData("8000000000000000000000000000000000")]
    [InlineData("ffffffffffffffffffffffffffffffffff")]
    public void BinaryTimestampsAboveTheMaximumAreRejected(string binHex)
    {
        byte[] binary = Hex.Parse(binHex);

        Assert.Equal(PikaidId.BinaryLength, binary.Length);
        Assert.False(PikaidId.TryFromBytes(binary, out PikaidId result));
        Assert.Equal(default, result);
        Assert.Throws<PikaidFormatException>(() => PikaidId.FromBytes(binary));
    }

    [Fact]
    public void MaximumValidBinaryTimestampIsAccepted()
    {
        byte[] binary = Hex.Parse("123ede3fffffffffffffffffffffffffff");

        Assert.True(PikaidId.TryFromBytes(binary, out PikaidId id));
        Assert.Equal(PikaidId.MaxTimestamp, id.Timestamp);
        Assert.Equal("zzzzzzz7oiylpimjg5u2ca1ypr", id.ToString());
    }

    [Fact]
    public void BinaryValueJustBelowTheTimestampMaximumIsAccepted()
    {
        byte[] binary = Hex.Parse("123ede3ffeffffffffffffffffffffffff");

        Assert.True(PikaidId.TryFromBytes(binary, out PikaidId id));
        Assert.Equal(PikaidId.MaxTimestamp - 1, id.Timestamp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(18)]
    public void WriteBytesRejectsDestinationsOfTheWrongLength(int length)
    {
        PikaidId id = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        byte[] destination = new byte[length];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => id.WriteBytes(destination));
        Assert.Equal("destination", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(13)]
    public void WriteRandomnessRejectsDestinationsOfTheWrongLength(int length)
    {
        PikaidId id = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        byte[] destination = new byte[length];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => id.WriteRandomness(destination));
        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void ParseSpanRejectsInvalidCharacters()
    {
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse("0000000000000000000000000-".AsSpan()));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void TryParseSpanReportsFailureAndReturnsDefault()
    {
        Assert.False(PikaidId.TryParse("not-a-pikaid".AsSpan(), out PikaidId result));
        Assert.Equal(default, result);

        Assert.True(PikaidId.TryParse("00000000000000000000000000".AsSpan(), out PikaidId valid));
        Assert.Equal("00000000000000000000000000", valid.ToString());
    }
}
