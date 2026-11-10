using System;
using System.Numerics;

namespace Pikaid.Tests;

/// <summary>
/// An independent Base36 encoder built on <see cref="BigInteger"/>.
/// It shares no code with the library under test and is used as a separate
/// oracle for the 7-character timestamp and 19-character randomness encodings.
/// </summary>
internal static class ReferenceBase36
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    public static string Encode(ulong value, int width)
    {
        Span<char> buffer = stackalloc char[width];
        buffer.Fill('0');

        ulong remaining = value;
        for (int i = width - 1; i >= 0; i--)
        {
            buffer[i] = Alphabet[(int)(remaining % 36)];
            remaining /= 36;
        }

        if (remaining != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The value does not fit the requested width.");
        }

        return new string(buffer);
    }

    public static string EncodeEntropy(ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length != PikaidId.RandomnessByteLength)
        {
            throw new ArgumentException("Entropy must be 12 bytes.", nameof(entropy));
        }

        BigInteger value = new(entropy, isUnsigned: true, isBigEndian: true);
        return EncodeBig(value, PikaidId.RandomnessLength);
    }

    public static string EncodeId(ulong timestamp, ReadOnlySpan<byte> entropy) =>
        Encode(timestamp, PikaidId.TimestampLength) + EncodeEntropy(entropy);

    public static string EncodeValue(BigInteger value, int width) => EncodeBig(value, width);

    public static BigInteger DecodeEntropy(string text)
    {
        if (text.Length != PikaidId.RandomnessLength)
        {
            throw new ArgumentException("The randomness field must be 19 characters.", nameof(text));
        }

        BigInteger value = BigInteger.Zero;
        foreach (char c in text)
        {
            value = (value * 36) + Digit(c);
        }

        return value;
    }

    private static string EncodeBig(BigInteger value, int width)
    {
        char[] buffer = new char[width];
        for (int i = width - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, 36, out BigInteger remainder);
            buffer[i] = Alphabet[(int)remainder];
        }

        if (value.Sign != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The value does not fit the requested width.");
        }

        return new string(buffer);
    }

    private static int Digit(char c)
    {
        if (c is >= '0' and <= '9')
        {
            return c - '0';
        }

        if (c is >= 'a' and <= 'z')
        {
            return c - 'a' + 10;
        }

        throw new ArgumentException("The text is not lowercase Base36.", nameof(c));
    }
}
