using System;
using System.Reflection;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Public API shape, constants and .NET conventions (value type semantics, equality,
/// exception types, documented defaults).
/// </summary>
public sealed class PublicApiTests
{
    private const string SampleId = "0s44we8002loz3po6mcf6vsn23";

    [Fact]
    public void PublishedConstantsMatchTheSpecification()
    {
        Assert.Equal(26, PikaidId.TextLength);
        Assert.Equal(17, PikaidId.BinaryLength);
        Assert.Equal(7, PikaidId.TimestampLength);
        Assert.Equal(19, PikaidId.RandomnessLength);
        Assert.Equal(5, PikaidId.TimestampByteLength);
        Assert.Equal(12, PikaidId.RandomnessByteLength);
        Assert.Equal(78_364_164_095UL, PikaidId.MaxTimestamp);
        Assert.Equal("0123456789abcdefghijklmnopqrstuvwxyz", PikaidId.Alphabet);
        Assert.Equal(36, PikaidId.Alphabet.Length);
    }

    [Fact]
    public void MaximumTimestampIsThirtySixToTheSeventhMinusOne()
    {
        ulong expected = 36;
        for (int i = 1; i < PikaidId.TimestampLength; i++)
        {
            expected *= 36;
        }

        expected -= 1;
        Assert.Equal(PikaidId.MaxTimestamp, expected);
        Assert.Equal("zzzzzzz", ReferenceBase36.Encode(PikaidId.MaxTimestamp, PikaidId.TimestampLength));
    }

    [Fact]
    public void DefaultValueIsTheCanonicalAllZeroIdentifier()
    {
        PikaidId id = default;

        Assert.Equal(0UL, id.Timestamp);
        Assert.Equal("000000000000000000000000", id.RandomnessHex);
        Assert.Equal("00000000000000000000000000", id.ToString());
        Assert.Equal(DateTimeOffset.UnixEpoch, id.TimestampUtc);
        Assert.True(PikaidId.IsValid(id.ToString()));
        Assert.Equal(PikaidId.Parse("00000000000000000000000000"), id);
        Assert.Equal(new byte[PikaidId.BinaryLength], id.ToByteArray());
    }

    [Fact]
    public void PikaidIdIsAnImmutableValueType()
    {
        Type type = typeof(PikaidId);

        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.False(type.IsClass);
        Assert.False(type.IsAbstract);
        Assert.Contains(typeof(IEquatable<PikaidId>), type.GetInterfaces());
        Assert.Contains(typeof(IComparable<PikaidId>), type.GetInterfaces());
        Assert.Contains(typeof(IComparable), type.GetInterfaces());
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.CanWrite);
    }

    [Fact]
    public void PublicGenerationAndConversionSurfaceIsPresent()
    {
        Type type = typeof(PikaidId);

        Assert.NotNull(type.GetMethod("New", Type.EmptyTypes));
        Assert.NotNull(type.GetMethod("New", new[] { typeof(ulong) }));
        Assert.NotNull(type.GetMethod("New", new[] { typeof(DateTimeOffset) }));
        Assert.NotNull(type.GetMethod("NewBytes", Type.EmptyTypes));
        Assert.NotNull(type.GetMethod("NewBytes", new[] { typeof(ulong) }));
        Assert.NotNull(type.GetMethod("Parse", new[] { typeof(string) }));
        Assert.NotNull(type.GetMethod("TryParse", new[] { typeof(string), typeof(PikaidId).MakeByRefType() }));
        Assert.NotNull(type.GetMethod("FromBytes", new[] { typeof(ReadOnlySpan<byte>) }));
        Assert.NotNull(type.GetMethod("ToByteArray", Type.EmptyTypes));
    }

    [Fact]
    public void ValueAndToStringAreTheSameCanonicalText()
    {
        PikaidId id = PikaidId.Parse(SampleId);

        Assert.Equal(SampleId, id.Value);
        Assert.Equal(SampleId, id.ToString());
        Assert.Equal(id.Value, id.ToString());
    }

    [Fact]
    public void EqualsObjectRejectsNullAndForeignTypes()
    {
        PikaidId id = PikaidId.Parse(SampleId);

        Assert.False(id.Equals(null));
        Assert.False(id.Equals(SampleId));
        Assert.False(id.Equals((object)42));
        Assert.True(id.Equals((object)PikaidId.Parse(SampleId)));
        Assert.True(id.Equals(PikaidId.Parse(SampleId)));
    }

    [Fact]
    public void EqualValuesHaveEqualHashes()
    {
        PikaidId left = PikaidId.Parse(SampleId);
        PikaidId right = PikaidId.FromBytes(Hex.Parse("006553f10000112233445566778899aabb"));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void FormatExceptionTypeIsCatchableAsFormatException()
    {
        Assert.True(typeof(FormatException).IsAssignableFrom(typeof(PikaidFormatException)));
        Assert.Equal(typeof(FormatException), typeof(PikaidFormatException).BaseType);
    }

    [Fact]
    public void FormatExceptionExposesTheUsualConstructors()
    {
        PikaidFormatException empty = new();
        PikaidFormatException withMessage = new("bad identifier");
        PikaidFormatException withInner = new("bad identifier", new InvalidOperationException("inner"));

        Assert.NotNull(empty.Message);
        Assert.Equal("bad identifier", withMessage.Message);
        Assert.Equal("bad identifier", withInner.Message);
        Assert.IsType<InvalidOperationException>(withInner.InnerException);

        try
        {
            PikaidId.Parse("not-a-pikaid");
            Assert.Fail("Parse should have thrown.");
        }
        catch (FormatException exception)
        {
            Assert.IsType<PikaidFormatException>(exception);
        }
    }

    [Fact]
    public void ParseStringAndSpanOverloadsAgree()
    {
        string text = "zzzzzzz7oiylpimjg5u2ca1ypr";

        PikaidId fromString = PikaidId.Parse(text);
        PikaidId fromSpan = PikaidId.Parse(text.AsSpan());

        Assert.Equal(fromString, fromSpan);
    }

    [Fact]
    public void TryParseOverloadsAgree()
    {
        string text = "01010101010101010101010101";

        Assert.True(PikaidId.TryParse(text, out PikaidId fromString));
        Assert.True(PikaidId.TryParse(text.AsSpan(), out PikaidId fromSpan));
        Assert.Equal(fromString, fromSpan);
    }

    [Fact]
    public void TimestampAndRandomnessAccessorsArePrecise()
    {
        PikaidId id = PikaidId.Parse(SampleId);

        Assert.Equal(1_700_000_000UL, id.Timestamp);
        Assert.Equal("00112233445566778899aabb", id.RandomnessHex);
        Assert.Equal(24, id.RandomnessHex.Length);
        Assert.All(id.RandomnessHex, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
        Assert.Equal(Hex.Parse(id.RandomnessHex), id.ToRandomness());
        Assert.Equal((ulong)id.TimestampUtc.ToUnixTimeSeconds(), id.Timestamp);
    }

    [Fact]
    public void StructDoesNotExposeMutableState()
    {
        PikaidId id = PikaidId.Parse(SampleId);
        PikaidId copy = id;

        Assert.Equal(id, copy);

        PropertyInfo[] properties = typeof(PikaidId)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.All(properties, property => Assert.False(property.CanWrite));
    }
}
