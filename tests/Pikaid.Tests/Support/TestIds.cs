using System;

namespace Pikaid.Tests;

internal static class TestIds
{
    public static byte[] Binary(ulong timestamp, ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length != PikaidId.RandomnessByteLength)
        {
            throw new ArgumentException("Entropy must be 12 bytes.", nameof(entropy));
        }

        byte[] binary = new byte[PikaidId.BinaryLength];
        binary[0] = (byte)(timestamp >> 32);
        binary[1] = (byte)(timestamp >> 24);
        binary[2] = (byte)(timestamp >> 16);
        binary[3] = (byte)(timestamp >> 8);
        binary[4] = (byte)timestamp;
        entropy.CopyTo(binary.AsSpan(PikaidId.TimestampByteLength));
        return binary;
    }

    public static PikaidId FromParts(ulong timestamp, string entropyHex) =>
        PikaidId.FromBytes(Binary(timestamp, Hex.Parse(entropyHex)));

    public static string RandomTimestamp(Random random) =>
        ReferenceBase36.Encode((ulong)random.NextInt64(0, (long)PikaidId.MaxTimestamp + 1), PikaidId.TimestampLength);

    public static string RandomEntropy(Random random)
    {
        Span<byte> entropy = stackalloc byte[PikaidId.RandomnessByteLength];
        random.NextBytes(entropy);
        return ReferenceBase36.EncodeEntropy(entropy);
    }

    public static string RandomText(Random random) => RandomTimestamp(random) + RandomEntropy(random);

    public static PikaidId RandomId(Random random) => PikaidId.Parse(RandomText(random));
}
