using System;

namespace Pikaid;

/// <summary>
/// Represents a pikaid: a 26-character, chronologically sortable identifier made of a
/// 7-character lowercase Base36 Unix timestamp and a 19-character Base36 encoding of
/// 96 bits of cryptographic randomness.
/// </summary>
/// <remarks>
/// <para>
/// The textual form is exactly 26 lowercase ASCII Base36 characters
/// (<c>[0-9a-z]</c>). The binary form is exactly 17 bytes: a 5-byte unsigned
/// big-endian timestamp followed by 12 unchanged randomness bytes. Both forms are
/// immutable and round-trip losslessly.
/// </para>
/// <para>
/// The identifier format is immutable and unversioned. The canonical definition is
/// <see href="https://github.com/pikaid/pikaid-specs/tree/v1.0.4">pikaid specification
/// revision 1.0.4</see>.
/// </para>
/// <para>
/// A pikaid is not a secret, authentication token, or authorization mechanism, and it
/// does not guarantee uniqueness. Values with different timestamps sort chronologically;
/// values generated within the same second are ordered by randomness, not by generation
/// order. Timestamp resolution is one second.
/// </para>
/// <para>
/// The <see langword="default"/> value of this type is the valid all-zero identifier
/// <c>00000000000000000000000000</c> (timestamp 0, all-zero randomness).
/// </para>
/// </remarks>
public readonly struct PikaidId : IEquatable<PikaidId>, IComparable<PikaidId>, IComparable
{
    /// <summary>The number of characters in the canonical textual form (26).</summary>
    public const int TextLength = 26;

    /// <summary>The number of bytes in the binary form (17).</summary>
    public const int BinaryLength = 17;

    /// <summary>The number of textual characters used by the timestamp (7).</summary>
    public const int TimestampLength = 7;

    /// <summary>The number of textual characters used by the randomness field (19).</summary>
    public const int RandomnessLength = 19;

    /// <summary>The number of timestamp bytes in the binary form (5).</summary>
    public const int TimestampByteLength = 5;

    /// <summary>The number of randomness bytes (12).</summary>
    public const int RandomnessByteLength = 12;

    /// <summary>The inclusive maximum timestamp value, <c>36^7 - 1</c> (78,364,164,095).</summary>
    public const ulong MaxTimestamp = 78_364_164_095UL;

    /// <summary>The permitted lowercase Base36 alphabet in ascending digit order.</summary>
    public const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    private const ulong TimestampRadix = 36UL;
    private const string HexDigits = "0123456789abcdef";

    private readonly ulong _timestamp;
    private readonly uint _randomness0;
    private readonly uint _randomness1;
    private readonly uint _randomness2;

    private PikaidId(ulong timestamp, uint randomness0, uint randomness1, uint randomness2)
    {
        _timestamp = timestamp;
        _randomness0 = randomness0;
        _randomness1 = randomness1;
        _randomness2 = randomness2;
    }

    /// <summary>
    /// Gets the Unix timestamp in whole seconds since <c>1970-01-01T00:00:00Z</c>.
    /// </summary>
    /// <value>A value in the inclusive range <c>0..78,364,164,095</c>.</value>
    public ulong Timestamp => _timestamp;

    /// <summary>
    /// Gets the timestamp as a UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <value>A UTC value whose offset is <see cref="TimeSpan.Zero"/>; resolution is one second.</value>
    public DateTimeOffset TimestampUtc => DateTimeOffset.FromUnixTimeSeconds(checked((long)_timestamp));

    /// <summary>
    /// Gets the 12 randomness bytes as a 24-character lowercase hexadecimal string.
    /// </summary>
    /// <value>Exactly 24 characters in <c>[0-9a-f]</c>.</value>
    /// <remarks>
    /// This is the hexadecimal form used by the canonical conformance vectors. Use
    /// <see cref="ToRandomness"/> or <see cref="WriteRandomness"/> for the raw bytes.
    /// </remarks>
    public string RandomnessHex
    {
        get
        {
            Span<byte> randomness = stackalloc byte[RandomnessByteLength];
            WriteRandomness(randomness);

            char[] chars = new char[RandomnessByteLength * 2];
            for (int i = 0; i < randomness.Length; i++)
            {
                chars[i * 2] = HexDigits[randomness[i] >> 4];
                chars[(i * 2) + 1] = HexDigits[randomness[i] & 0x0F];
            }

            return new string(chars);
        }
    }

    /// <summary>
    /// Gets the canonical 26-character textual representation.
    /// </summary>
    /// <value>Exactly 26 lowercase Base36 characters.</value>
    public string Value => ToString();

    /// <summary>
    /// Creates a new pikaid using the current UTC time and a fresh 96-bit value from the
    /// operating system cryptographic random number generator.
    /// </summary>
    /// <returns>A newly generated pikaid.</returns>
    /// <exception cref="InvalidOperationException">
    /// The system clock is before the Unix epoch or beyond <see cref="MaxTimestamp"/>.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The cryptographic random number generator failed. Generation fails closed; no
    /// identifier is returned.
    /// </exception>
    public static PikaidId New() => New(CurrentUnixSeconds());

    /// <summary>
    /// Creates a new pikaid with an explicitly supplied whole-second Unix timestamp and a
    /// fresh 96-bit value from the operating system cryptographic random number generator.
    /// </summary>
    /// <param name="unixSeconds">Whole Unix seconds in the range <c>0..78,364,164,095</c>.</param>
    /// <returns>A newly generated pikaid.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="unixSeconds"/> is greater than <see cref="MaxTimestamp"/>.
    /// (Unsigned values cannot be negative.)
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The cryptographic random number generator failed. Generation fails closed.
    /// </exception>
    public static PikaidId New(ulong unixSeconds)
    {
        if (unixSeconds > MaxTimestamp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unixSeconds),
                unixSeconds,
                FormattableString.Invariant(
                    $"The timestamp must be between 0 and {MaxTimestamp} inclusive."));
        }

        Span<byte> randomness = stackalloc byte[RandomnessByteLength];
        PikaidEntropy.Fill(randomness);
        return Create(unixSeconds, randomness);
    }

    /// <summary>
    /// Creates a new pikaid with a timestamp taken from a <see cref="DateTimeOffset"/> and a
    /// fresh 96-bit value from the operating system cryptographic random number generator.
    /// </summary>
    /// <param name="timestamp">
    /// The instant to encode. It is converted to UTC. It must be a whole number of seconds
    /// in the range <c>1970-01-01T00:00:00Z..4453-04-05T15:21:35Z</c>.
    /// </param>
    /// <returns>A newly generated pikaid.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="timestamp"/> has sub-second precision. It is rejected rather than
    /// silently truncated.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timestamp"/> is before the Unix epoch or beyond <see cref="MaxTimestamp"/>.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The cryptographic random number generator failed. Generation fails closed.
    /// </exception>
    public static PikaidId New(DateTimeOffset timestamp)
    {
        DateTimeOffset utc = timestamp.ToUniversalTime();
        if (utc.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException(
                "The timestamp must be a whole number of seconds; sub-second precision is not supported.",
                nameof(timestamp));
        }

        long unixSeconds = utc.ToUnixTimeSeconds();
        if (unixSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timestamp),
                timestamp,
                "The timestamp must not be before the Unix epoch.");
        }

        if (unixSeconds > (long)MaxTimestamp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timestamp),
                timestamp,
                FormattableString.Invariant(
                    $"The timestamp must not be later than {MaxTimestamp} Unix seconds."));
        }

        return New((ulong)unixSeconds);
    }

    /// <summary>
    /// Creates a new pikaid from the current UTC time and returns its 17-byte binary form.
    /// </summary>
    /// <returns>A newly generated 17-byte binary pikaid.</returns>
    /// <exception cref="InvalidOperationException">
    /// The system clock is before the Unix epoch or beyond <see cref="MaxTimestamp"/>.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The cryptographic random number generator failed. Generation fails closed.
    /// </exception>
    public static byte[] NewBytes() => New().ToByteArray();

    /// <summary>
    /// Creates a new pikaid with an explicitly supplied whole-second Unix timestamp and
    /// returns its 17-byte binary form.
    /// </summary>
    /// <param name="unixSeconds">Whole Unix seconds in the range <c>0..78,364,164,095</c>.</param>
    /// <returns>A newly generated 17-byte binary pikaid.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="unixSeconds"/> is greater than <see cref="MaxTimestamp"/>.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The cryptographic random number generator failed. Generation fails closed.
    /// </exception>
    public static byte[] NewBytes(ulong unixSeconds) => New(unixSeconds).ToByteArray();

    /// <summary>
    /// Reports whether a value is a fully valid pikaid: the exact length, the exact
    /// lowercase Base36 alphabet, a timestamp within range, and randomness within 96 bits.
    /// </summary>
    /// <param name="value">The candidate value. <see langword="null"/> is not valid.</param>
    /// <returns><see langword="true"/> only when the value is a fully valid pikaid.</returns>
    public static bool IsValid(string? value) => TryParse(value, out _);

    /// <summary>
    /// Reports whether a value has the pikaid shape: exactly 26 characters from the
    /// lowercase Base36 alphabet. This is a fast format-only check.
    /// </summary>
    /// <param name="value">The candidate value. <see langword="null"/> is not well formed.</param>
    /// <returns><see langword="true"/> when length and alphabet match.</returns>
    /// <remarks>
    /// This does not validate the 96-bit randomness limit, so a well-formed value can still
    /// be invalid. Use <see cref="IsValid"/> or <see cref="TryParse(string?, out PikaidId)"/>
    /// for full validation.
    /// </remarks>
    public static bool IsWellFormed(string? value)
    {
        if (value is null || value.Length != TextLength)
        {
            return false;
        }

        return IsWellFormed(value.AsSpan());
    }

    /// <summary>
    /// Reports whether a character span has the pikaid shape: exactly 26 characters from
    /// the lowercase Base36 alphabet. This is a fast format-only check.
    /// </summary>
    /// <param name="value">The candidate characters.</param>
    /// <returns><see langword="true"/> when length and alphabet match.</returns>
    /// <remarks>
    /// This does not validate the 96-bit randomness limit. Use
    /// <see cref="TryParse(ReadOnlySpan{char}, out PikaidId)"/> for full validation.
    /// </remarks>
    public static bool IsWellFormed(ReadOnlySpan<char> value)
    {
        if (value.Length != TextLength)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (DecodeDigit(value[i]) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tries to parse a fully valid pikaid from its 26-character textual form.
    /// </summary>
    /// <param name="value">The candidate value. <see langword="null"/> is not valid.</param>
    /// <param name="result">The parsed pikaid when successful; otherwise the default value.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a fully valid pikaid.</returns>
    public static bool TryParse(string? value, out PikaidId result)
    {
        if (value is null)
        {
            result = default;
            return false;
        }

        return TryParse(value.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a fully valid pikaid from its 26-character textual form.
    /// </summary>
    /// <param name="value">The candidate characters.</param>
    /// <param name="result">The parsed pikaid when successful; otherwise the default value.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a fully valid pikaid.</returns>
    public static bool TryParse(ReadOnlySpan<char> value, out PikaidId result)
    {
        result = default;

        if (value.Length != TextLength)
        {
            return false;
        }

        ulong timestamp = 0;
        for (int i = 0; i < TimestampLength; i++)
        {
            int digit = DecodeDigit(value[i]);
            if (digit < 0)
            {
                return false;
            }

            timestamp = (timestamp * TimestampRadix) + (uint)digit;
        }

        if (timestamp > MaxTimestamp)
        {
            return false;
        }

        Span<byte> randomness = stackalloc byte[RandomnessByteLength];
        randomness.Clear();

        for (int i = 0; i < RandomnessLength; i++)
        {
            int digit = DecodeDigit(value[TimestampLength + i]);
            if (digit < 0)
            {
                return false;
            }

            int carry = digit;
            for (int j = RandomnessByteLength - 1; j >= 0; j--)
            {
                int current = (randomness[j] * 36) + carry;
                randomness[j] = (byte)current;
                carry = current >> 8;
            }

            if (carry != 0)
            {
                // The decoded randomness needs more than 96 bits.
                return false;
            }
        }

        result = Create(timestamp, randomness);
        return true;
    }

    /// <summary>
    /// Parses a fully valid pikaid from its 26-character textual form.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The parsed pikaid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="PikaidFormatException">
    /// <paramref name="value"/> is not exactly 26 lowercase Base36 characters, or its
    /// randomness exceeds 96 bits.
    /// </exception>
    public static PikaidId Parse(string value)
    {
#if NET
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        if (!TryParse(value.AsSpan(), out PikaidId result))
        {
            throw new PikaidFormatException(
                "The value is not a valid pikaid. Expected exactly 26 lowercase Base36 characters "
                + "with a timestamp in 0..78364164095 and randomness within 96 bits.");
        }

        return result;
    }

    /// <summary>
    /// Parses a fully valid pikaid from its 26-character textual form.
    /// </summary>
    /// <param name="value">The candidate characters.</param>
    /// <returns>The parsed pikaid.</returns>
    /// <exception cref="PikaidFormatException">
    /// <paramref name="value"/> is not exactly 26 lowercase Base36 characters, or its
    /// randomness exceeds 96 bits.
    /// </exception>
    public static PikaidId Parse(ReadOnlySpan<char> value)
    {
        if (!TryParse(value, out PikaidId result))
        {
            throw new PikaidFormatException(
                "The value is not a valid pikaid. Expected exactly 26 lowercase Base36 characters "
                + "with a timestamp in 0..78364164095 and randomness within 96 bits.");
        }

        return result;
    }

    /// <summary>
    /// Tries to decode a pikaid from its exactly 17-byte binary form.
    /// </summary>
    /// <param name="bytes">The binary value.</param>
    /// <param name="result">The decoded pikaid when successful; otherwise the default value.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="bytes"/> is exactly 17 bytes and its
    /// timestamp is within range.
    /// </returns>
    public static bool TryFromBytes(ReadOnlySpan<byte> bytes, out PikaidId result)
    {
        result = default;

        if (bytes.Length != BinaryLength)
        {
            return false;
        }

        ulong timestamp = ((ulong)bytes[0] << 32)
            | ((ulong)bytes[1] << 24)
            | ((ulong)bytes[2] << 16)
            | ((ulong)bytes[3] << 8)
            | bytes[4];

        if (timestamp > MaxTimestamp)
        {
            return false;
        }

        result = Create(timestamp, bytes.Slice(TimestampByteLength, RandomnessByteLength));
        return true;
    }

    /// <summary>
    /// Decodes a pikaid from its exactly 17-byte binary form.
    /// </summary>
    /// <param name="bytes">The binary value.</param>
    /// <returns>The decoded pikaid.</returns>
    /// <exception cref="PikaidFormatException">
    /// <paramref name="bytes"/> is not exactly 17 bytes, or its timestamp exceeds
    /// <see cref="MaxTimestamp"/>.
    /// </exception>
    public static PikaidId FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (!TryFromBytes(bytes, out PikaidId result))
        {
            if (bytes.Length != BinaryLength)
            {
                throw new PikaidFormatException(
                    FormattableString.Invariant(
                        $"A binary pikaid must be exactly {BinaryLength} bytes; got {bytes.Length}."));
            }

            throw new PikaidFormatException(
                FormattableString.Invariant(
                    $"The binary pikaid timestamp exceeds the maximum of {MaxTimestamp} Unix seconds."));
        }

        return result;
    }

    /// <summary>
    /// Copies the 12 randomness bytes, in the original order, into a new array.
    /// </summary>
    /// <returns>A new 12-byte array.</returns>
    public byte[] ToRandomness()
    {
        byte[] randomness = new byte[RandomnessByteLength];
        WriteRandomness(randomness);
        return randomness;
    }

    /// <summary>
    /// Writes the 12 randomness bytes, in the original order, into a destination span.
    /// </summary>
    /// <param name="destination">A destination of exactly 12 bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is not exactly 12 bytes long.
    /// </exception>
    public void WriteRandomness(Span<byte> destination)
    {
        if (destination.Length != RandomnessByteLength)
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"The destination must be exactly {RandomnessByteLength} bytes long."),
                nameof(destination));
        }

        WriteUInt32BigEndian(destination.Slice(0, 4), _randomness0);
        WriteUInt32BigEndian(destination.Slice(4, 4), _randomness1);
        WriteUInt32BigEndian(destination.Slice(8, 4), _randomness2);
    }

    /// <summary>
    /// Returns the 17-byte binary representation.
    /// </summary>
    /// <returns>
    /// A new 17-byte array: 5 unsigned big-endian timestamp bytes followed by the
    /// 12 unchanged randomness bytes.
    /// </returns>
    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[BinaryLength];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Writes the 17-byte binary representation into a destination span.
    /// </summary>
    /// <param name="destination">A destination of exactly 17 bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is not exactly 17 bytes long.
    /// </exception>
    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length != BinaryLength)
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"The destination must be exactly {BinaryLength} bytes long."),
                nameof(destination));
        }

        destination[0] = (byte)(_timestamp >> 32);
        destination[1] = (byte)(_timestamp >> 24);
        destination[2] = (byte)(_timestamp >> 16);
        destination[3] = (byte)(_timestamp >> 8);
        destination[4] = (byte)_timestamp;
        WriteRandomness(destination.Slice(TimestampByteLength, RandomnessByteLength));
    }

    /// <summary>
    /// Compares this pikaid with another by timestamp and then by unsigned randomness.
    /// </summary>
    /// <param name="other">The other pikaid.</param>
    /// <returns>
    /// A negative value when this instance sorts first, zero when they are equal, and a
    /// positive value when this instance sorts last.
    /// </returns>
    /// <remarks>
    /// The order is identical to ASCII/ordinal comparison of the textual forms and to
    /// unsigned bytewise comparison of the 17-byte binary forms. Values with different
    /// timestamps sort chronologically; values generated within the same second are not in
    /// generation order.
    /// </remarks>
    public int CompareTo(PikaidId other)
    {
        int comparison = _timestamp.CompareTo(other._timestamp);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = _randomness0.CompareTo(other._randomness0);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = _randomness1.CompareTo(other._randomness1);
        if (comparison != 0)
        {
            return comparison;
        }

        return _randomness2.CompareTo(other._randomness2);
    }

    int IComparable.CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is PikaidId other)
        {
            return CompareTo(other);
        }

        throw new ArgumentException(
            FormattableString.Invariant(
                $"The object must be of type {nameof(PikaidId)}."),
            nameof(obj));
    }

    /// <summary>
    /// Determines whether this pikaid equals another pikaid.
    /// </summary>
    /// <param name="other">The other pikaid.</param>
    /// <returns><see langword="true"/> when both timestamp and randomness are equal.</returns>
    public bool Equals(PikaidId other) =>
        _timestamp == other._timestamp
        && _randomness0 == other._randomness0
        && _randomness1 == other._randomness1
        && _randomness2 == other._randomness2;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PikaidId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + _timestamp.GetHashCode();
            hash = (hash * 31) + _randomness0.GetHashCode();
            hash = (hash * 31) + _randomness1.GetHashCode();
            hash = (hash * 31) + _randomness2.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// Returns the canonical 26-character textual representation.
    /// </summary>
    /// <returns>Exactly 26 lowercase Base36 characters.</returns>
    public override string ToString()
    {
        char[] chars = new char[TextLength];

        ulong timestamp = _timestamp;
        for (int i = TimestampLength - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(timestamp % TimestampRadix)];
            timestamp /= TimestampRadix;
        }

        Span<byte> work = stackalloc byte[RandomnessByteLength];
        WriteRandomness(work);

        for (int i = TextLength - 1; i >= TimestampLength; i--)
        {
            int remainder = 0;
            for (int j = 0; j < RandomnessByteLength; j++)
            {
                int current = (remainder << 8) | work[j];
                work[j] = (byte)(current / 36);
                remainder = current % 36;
            }

            chars[i] = Alphabet[remainder];
        }

        return new string(chars);
    }

    /// <summary>
    /// Determines whether two pikaid values are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both values are equal.</returns>
    public static bool operator ==(PikaidId left, PikaidId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two pikaid values are different.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the values differ.</returns>
    public static bool operator !=(PikaidId left, PikaidId right) => !left.Equals(right);

    /// <summary>
    /// Determines whether the left pikaid sorts before the right pikaid.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> sorts first.</returns>
    public static bool operator <(PikaidId left, PikaidId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether the left pikaid sorts after the right pikaid.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> sorts last.</returns>
    public static bool operator >(PikaidId left, PikaidId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether the left pikaid sorts before or equal to the right pikaid.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> does not sort after.</returns>
    public static bool operator <=(PikaidId left, PikaidId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether the left pikaid sorts after or equal to the right pikaid.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> does not sort before.</returns>
    public static bool operator >=(PikaidId left, PikaidId right) => left.CompareTo(right) >= 0;

    private static PikaidId Create(ulong timestamp, ReadOnlySpan<byte> randomness)
    {
        return new PikaidId(
            timestamp,
            ReadUInt32BigEndian(randomness.Slice(0, 4)),
            ReadUInt32BigEndian(randomness.Slice(4, 4)),
            ReadUInt32BigEndian(randomness.Slice(8, 4)));
    }

    private static ulong CurrentUnixSeconds()
    {
        long unixSeconds = PikaidClock.UtcNowUnixSeconds();
        if (unixSeconds < 0)
        {
            throw new InvalidOperationException(
                "The system clock is before the Unix epoch; a pikaid with a negative timestamp cannot be generated.");
        }

        if (unixSeconds > (long)MaxTimestamp)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant(
                    $"The system clock is beyond the maximum pikaid timestamp of {MaxTimestamp} Unix seconds."));
        }

        return (ulong)unixSeconds;
    }

    private static int DecodeDigit(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'a' && value <= 'z')
        {
            return value - 'a' + 10;
        }

        return -1;
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> bytes)
    {
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }
}
