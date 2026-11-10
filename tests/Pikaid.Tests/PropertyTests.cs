using System;
using System.Collections.Generic;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Seeded property-style tests that compare the library against the independent
/// <see cref="ReferenceBase36"/> oracle across many generated inputs.
/// </summary>
public sealed class PropertyTests
{
    [Fact]
    public void RandomBinaryValuesRoundTripThroughTextAndBinary()
    {
        Random random = new(20261106);

        for (int i = 0; i < 2000; i++)
        {
            PikaidId original = RandomValidId(random);

            string text = original.ToString();
            byte[] binary = original.ToByteArray();

            Assert.Equal(PikaidId.TextLength, text.Length);
            Assert.Equal(PikaidId.BinaryLength, binary.Length);

            PikaidId fromText = PikaidId.Parse(text);
            PikaidId fromBinary = PikaidId.FromBytes(binary);

            Assert.Equal(original, fromText);
            Assert.Equal(original, fromBinary);
            Assert.Equal(text, fromBinary.ToString());
            Assert.Equal(binary, fromText.ToByteArray());
            Assert.Equal(
                ReferenceBase36.EncodeEntropy(original.ToRandomness()),
                text.Substring(PikaidId.TimestampLength));
        }
    }

    [Fact]
    public void TextEncodingAgreesWithTheIndependentBigIntegerOracle()
    {
        Random random = new(7);

        for (int i = 0; i < 1000; i++)
        {
            ulong timestamp = NextTimestamp(random);
            byte[] entropy = NextEntropy(random);

            PikaidId id = TestIds.FromParts(timestamp, Hex.Format(entropy));

            string expected = ReferenceBase36.EncodeId(timestamp, entropy);
            Assert.Equal(expected, id.ToString());

            // Decoding the oracle-produced text must reproduce the same fields.
            PikaidId parsed = PikaidId.Parse(expected);
            Assert.Equal(timestamp, parsed.Timestamp);
            Assert.Equal(Hex.Format(entropy), parsed.RandomnessHex);
            Assert.Equal(id, parsed);
        }
    }

    [Fact]
    public void RandomValidTextParsesAndReproducesItsFields()
    {
        Random random = new(11);

        for (int i = 0; i < 1000; i++)
        {
            string text = TestIds.RandomText(random);

            Assert.True(PikaidId.IsWellFormed(text));
            Assert.True(PikaidId.IsValid(text));
            Assert.True(PikaidId.TryParse(text, out PikaidId id));

            Assert.Equal(text, id.ToString());
            Assert.Equal(ReferenceBase36.Encode(id.Timestamp, PikaidId.TimestampLength), text[..PikaidId.TimestampLength]);
            Assert.Equal(ReferenceBase36.EncodeEntropy(Hex.Parse(id.RandomnessHex)), text[PikaidId.TimestampLength..]);
            Assert.Equal(id.TimestampUtc, DateTimeOffset.FromUnixTimeSeconds((long)id.Timestamp));
        }
    }

    [Fact]
    public void RandomTwentySixCharacterBase36TextIsAcceptedExactlyWhenRandomnessFitsNinetySixBits()
    {
        Random random = new(13);
        int accepted = 0;
        int rejected = 0;

        for (int i = 0; i < 1000; i++)
        {
            string text = RandomBase36Text(random);

            bool fits = ReferenceBase36.DecodeEntropy(text[PikaidId.TimestampLength..])
                <= ((System.Numerics.BigInteger.One << 96) - 1);

            Assert.Equal(fits, PikaidId.TryParse(text, out PikaidId parsed));
            Assert.True(PikaidId.IsWellFormed(text));
            Assert.Equal(fits, PikaidId.IsValid(text));

            if (fits)
            {
                accepted++;
                Assert.Equal(text, parsed.ToString());
            }
            else
            {
                rejected++;
                Assert.Equal(default, parsed);
                Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(text));
            }
        }

        // Both branches must be exercised for the property to be meaningful.
        Assert.True(accepted > 0);
        Assert.True(rejected > 0);
    }

    [Fact]
    public void RandomBinaryValuesKeepTheirFiveByteTimestampPrefix()
    {
        Random random = new(17);

        for (int i = 0; i < 500; i++)
        {
            PikaidId id = RandomValidId(random);
            byte[] binary = id.ToByteArray();

            ulong decoded = ((ulong)binary[0] << 32)
                | ((ulong)binary[1] << 24)
                | ((ulong)binary[2] << 16)
                | ((ulong)binary[3] << 8)
                | binary[4];

            Assert.Equal(id.Timestamp, decoded);
            Assert.Equal(id.Timestamp, (ulong)id.TimestampUtc.ToUnixTimeSeconds());
        }
    }

    [Fact]
    public void RandomValuesSortIdenticallyAsTextBinaryAndStruct()
    {
        Random random = new(19);
        List<PikaidId> ids = new();

        for (int i = 0; i < 500; i++)
        {
            ids.Add(RandomValidId(random));
        }

        for (int i = 1; i < ids.Count; i++)
        {
            PikaidId left = ids[i - 1];
            PikaidId right = ids[i];

            int structured = Math.Sign(left.CompareTo(right));
            int text = Math.Sign(string.CompareOrdinal(left.ToString(), right.ToString()));
            int binary = Math.Sign(left.ToByteArray().AsSpan().SequenceCompareTo(right.ToByteArray()));

            Assert.Equal(structured, text);
            Assert.Equal(structured, binary);
        }
    }

    [Fact]
    public void RandomInvalidCharactersAreAlwaysRejectedAndNeverNormalized()
    {
        Random random = new(23);

        for (int i = 0; i < 500; i++)
        {
            char[] text = RandomBase36Text(random).ToCharArray();
            int index = random.Next(text.Length);

            char replacement;
            do
            {
                replacement = (char)random.Next(0x20, 0x7F);
            }
            while (replacement is >= '0' and <= '9' or >= 'a' and <= 'z');

            text[index] = replacement;
            string candidate = new(text);

            Assert.False(PikaidId.IsWellFormed(candidate));
            Assert.False(PikaidId.IsValid(candidate));
            Assert.False(PikaidId.TryParse(candidate, out PikaidId result));
            Assert.Equal(default, result);
            Assert.Throws<PikaidFormatException>(() => PikaidId.Parse(candidate));
        }
    }

    private static PikaidId RandomValidId(Random random)
    {
        ulong timestamp = NextTimestamp(random);
        byte[] entropy = new byte[PikaidId.RandomnessByteLength];
        random.NextBytes(entropy);
        return TestIds.FromParts(timestamp, Hex.Format(entropy));
    }

    private static ulong NextTimestamp(Random random) =>
        (ulong)random.NextInt64(0, (long)PikaidId.MaxTimestamp + 1);

    private static byte[] NextEntropy(Random random)
    {
        byte[] entropy = new byte[PikaidId.RandomnessByteLength];
        random.NextBytes(entropy);
        return entropy;
    }

    private static string RandomBase36Text(Random random)
    {
        char[] chars = new char[PikaidId.TextLength];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = PikaidId.Alphabet[random.Next(PikaidId.Alphabet.Length)];
        }

        return new string(chars);
    }
}
