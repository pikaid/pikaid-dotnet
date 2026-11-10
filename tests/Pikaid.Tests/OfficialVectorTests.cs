using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Runs every applicable vector from the official PHP v1.2.0 interoperability fixture.
/// The fixture is an independent artifact: none of these expectations are derived from
/// the implementation under test.
/// </summary>
public sealed class OfficialVectorTests
{
    private static VectorFixture Fixture => VectorFixture.Instance;

    private static ValidVector Valid(string name) =>
        Fixture.Valid.Single(vector => vector.Name == name);

    private static InvalidStringVector InvalidString(string name) =>
        Fixture.Invalid.String.Single(vector => vector.Name == name);

    private static InvalidBinaryVector InvalidBinary(string name) =>
        Fixture.Invalid.Binary.Single(vector => vector.Name == name);

    public static TheoryData<string> ValidVectorNames
    {
        get
        {
            TheoryData<string> data = new();
            foreach (ValidVector vector in Fixture.Valid)
            {
                data.Add(vector.Name);
            }

            return data;
        }
    }

    public static TheoryData<string> InvalidStringVectorNames
    {
        get
        {
            TheoryData<string> data = new();
            foreach (InvalidStringVector vector in Fixture.Invalid.String)
            {
                data.Add(vector.Name);
            }

            return data;
        }
    }

    public static TheoryData<string> InvalidBinaryVectorNames
    {
        get
        {
            TheoryData<string> data = new();
            foreach (InvalidBinaryVector vector in Fixture.Invalid.Binary)
            {
                data.Add(vector.Name);
            }

            return data;
        }
    }

    [Fact]
    public void FixtureMetadataMatchesSpecificationAndLibraryConstants()
    {
        Assert.Equal("pikaid", Fixture.Meta.Project);
        Assert.Equal(26, Fixture.Meta.StringLength);
        Assert.Equal(17, Fixture.Meta.BinaryLength);
        Assert.Equal("1.0.4", Fixture.Meta.SpecRevision);
        Assert.Equal(PikaidId.TextLength, Fixture.Meta.StringLength);
        Assert.Equal(PikaidId.BinaryLength, Fixture.Meta.BinaryLength);
        Assert.Equal(78_364_164_095UL, PikaidId.MaxTimestamp);
    }

    [Fact]
    public void FixtureContainsTheExpectedNumberOfVectors()
    {
        Assert.Equal(4, Fixture.Valid.Count);
        Assert.Equal(6, Fixture.Invalid.String.Count);
        Assert.Equal(4, Fixture.Invalid.Binary.Count);
    }

    [Theory]
    [MemberData(nameof(ValidVectorNames))]
    public void ValidVectorParsesToTheExpectedFields(string name)
    {
        ValidVector vector = Valid(name);

        PikaidId id = PikaidId.Parse(vector.Id);

        Assert.Equal(vector.Id, id.ToString());
        Assert.Equal(vector.Id, id.Value);
        Assert.Equal((ulong)vector.Timestamp, id.Timestamp);
        Assert.Equal(vector.EntropyHex, id.RandomnessHex);
        Assert.Equal(Hex.Format(id.ToByteArray()), vector.BinHex);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(vector.Timestamp), id.TimestampUtc);
        Assert.True(PikaidId.IsValid(vector.Id));
        Assert.True(PikaidId.IsWellFormed(vector.Id));
        Assert.True(PikaidId.TryParse(vector.Id, out PikaidId parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [MemberData(nameof(ValidVectorNames))]
    public void ValidVectorDecodesFromBinaryToTheExpectedFields(string name)
    {
        ValidVector vector = Valid(name);
        byte[] binary = Hex.Parse(vector.BinHex);

        PikaidId id = PikaidId.FromBytes(binary);

        Assert.Equal(vector.Id, id.ToString());
        Assert.Equal(vector.EntropyHex, id.RandomnessHex);
        Assert.Equal((ulong)vector.Timestamp, id.Timestamp);
        Assert.True(PikaidId.TryFromBytes(binary, out PikaidId decoded));
        Assert.Equal(id, decoded);
    }

    [Theory]
    [MemberData(nameof(ValidVectorNames))]
    public void ValidVectorRoundTripsBetweenTextAndBinary(string name)
    {
        ValidVector vector = Valid(name);

        PikaidId fromText = PikaidId.Parse(vector.Id);
        PikaidId fromBinary = PikaidId.FromBytes(Hex.Parse(vector.BinHex));

        Assert.Equal(fromText, fromBinary);
        Assert.Equal(vector.BinHex, Hex.Format(fromText.ToByteArray()));
        Assert.Equal(vector.Id, fromBinary.ToString());
    }

    [Theory]
    [MemberData(nameof(ValidVectorNames))]
    public void ValidVectorAgreesAcrossEveryPublicTextAndBinaryEntrypoint(string name)
    {
        ValidVector vector = Valid(name);
        byte[] binary = Hex.Parse(vector.BinHex);

        PikaidId fromString = PikaidId.Parse(vector.Id);
        PikaidId fromCharSpan = PikaidId.Parse(vector.Id.AsSpan());

        Assert.True(PikaidId.TryParse(vector.Id.AsSpan(), out PikaidId fromTrySpan));
        Assert.True(PikaidId.IsWellFormed(vector.Id.AsSpan()));
        Assert.True(PikaidId.TryFromBytes(binary.AsSpan(), out PikaidId fromByteSpan));

        Assert.Equal(fromString, fromCharSpan);
        Assert.Equal(fromString, fromTrySpan);
        Assert.Equal(fromString, fromByteSpan);
        Assert.Equal(vector.Id, fromCharSpan.Value);

        byte[] bytes = new byte[PikaidId.BinaryLength];
        fromCharSpan.WriteBytes(bytes);
        Assert.Equal(vector.BinHex, Hex.Format(bytes));

        byte[] randomness = new byte[PikaidId.RandomnessByteLength];
        fromCharSpan.WriteRandomness(randomness);
        Assert.Equal(vector.EntropyHex, Hex.Format(randomness));
        Assert.Equal(vector.EntropyHex, Hex.Format(fromCharSpan.ToRandomness()));
    }

    [Theory]
    [MemberData(nameof(InvalidStringVectorNames))]
    public void InvalidStringVectorIsRejectedByEverySpanEntrypoint(string name)
    {
        InvalidStringVector vector = InvalidString(name);

        Assert.False(PikaidId.TryParse(vector.Id.AsSpan(), out PikaidId result));
        Assert.Equal(default, result);
        Assert.False(PikaidId.IsValid(vector.Id));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(vector.Id.AsSpan()));
        Assert.Equal(PikaidId.IsWellFormed(vector.Id), PikaidId.IsWellFormed(vector.Id.AsSpan()));
    }

    [Theory]
    [MemberData(nameof(InvalidStringVectorNames))]
    public void InvalidStringVectorIsRejectedByTryParse(string name)
    {
        InvalidStringVector vector = InvalidString(name);

        Assert.False(PikaidId.TryParse(vector.Id, out PikaidId result));
        Assert.Equal(default, result);
        Assert.False(PikaidId.IsValid(vector.Id));
        Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(vector.Id));
    }

    [Theory]
    [MemberData(nameof(InvalidStringVectorNames))]
    public void InvalidStringVectorShapeCheckIsAccurate(string name)
    {
        InvalidStringVector vector = InvalidString(name);

        // Shape checking is deliberately weaker than full validation. Only the
        // 96-bit randomness overflow is shaped correctly but semantically invalid.
        bool expectedWellFormed = vector.Name == "entropy-overflow";

        Assert.Equal(expectedWellFormed, PikaidId.IsWellFormed(vector.Id));
    }

    [Theory]
    [MemberData(nameof(InvalidBinaryVectorNames))]
    public void InvalidBinaryVectorIsRejectedByTryFromBytes(string name)
    {
        InvalidBinaryVector vector = InvalidBinary(name);
        byte[] binary = Hex.Parse(vector.BinHex);

        Assert.False(PikaidId.TryFromBytes(binary, out PikaidId result));
        Assert.Equal(default, result);
        Assert.Throws<PikaidFormatException>(() => PikaidId.FromBytes(binary));
    }

    [Fact]
    public void EntropyOverflowVectorIsShapedButNotSemanticallyValid()
    {
        const string overflow = "0000000zzzzzzzzzzzzzzzzzzz";

        Assert.True(PikaidId.IsWellFormed(overflow));
        Assert.False(PikaidId.IsValid(overflow));
    }

    [Fact]
    public void BinaryTimestampOverflowVectorHasTheExpectedFiveBytePrefix()
    {
        byte[] binary = Hex.Parse("123ede4000000000000000000000000000");

        Assert.Equal(PikaidId.BinaryLength, binary.Length);
        Assert.Equal(0x12, binary[0]);
        Assert.Equal(0x3E, binary[1]);
        Assert.Equal(0xDE, binary[2]);
        Assert.Equal(0x40, binary[3]);
        Assert.Equal(0x00, binary[4]);
    }

    [Fact]
    public void InvalidBinaryLengthsAreReportedAsFormatErrors()
    {
        PikaidFormatException exception = Assert.Throws<PikaidFormatException>(
            () => PikaidId.FromBytes(new byte[16]));

        Assert.Contains("17", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidBinaryTimestampIsReportedAsFormatError()
    {
        byte[] binary = new byte[PikaidId.BinaryLength];
        binary[0] = 0xFF;
        binary[1] = 0xFF;
        binary[2] = 0xFF;
        binary[3] = 0xFF;
        binary[4] = 0xFF;

        PikaidFormatException exception = Assert.Throws<PikaidFormatException>(
            () => PikaidId.FromBytes(binary));

        Assert.Contains("timestamp", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidVectorsAreRejectedWithoutThrowingFromShapeChecks()
    {
        List<string> invalid = new();
        foreach (InvalidStringVector vector in Fixture.Invalid.String)
        {
            invalid.Add(vector.Id);
        }

        Assert.All(invalid, value => Assert.False(PikaidId.IsValid(value)));
    }
}
