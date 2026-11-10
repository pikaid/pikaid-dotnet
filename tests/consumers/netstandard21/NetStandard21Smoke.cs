using System;

namespace Pikaid.ConsumerSmoke
{
    /// <summary>
    /// Compiles against the <c>netstandard2.1</c> asset of the packed package.
    /// </summary>
    public static class NetStandard21Smoke
    {
        /// <summary>
        /// Exercises parsing, binary conversion, generation and ordering through the public API.
        /// </summary>
        /// <param name="value">A canonical pikaid string.</param>
        /// <returns>The value produced by a lossless text and binary round trip.</returns>
        public static string RoundTrip(string value)
        {
            PikaidId parsed = PikaidId.Parse(value);

            byte[] bytes = new byte[PikaidId.BinaryLength];
            parsed.WriteBytes(bytes);
            Require(PikaidId.TryFromBytes(bytes, out PikaidId fromBinary), "binary decode failed");
            Require(fromBinary.Equals(parsed), "binary round trip mismatch");

            byte[] expectedRandomness = parsed.ToRandomness();
            byte[] randomness = new byte[PikaidId.RandomnessByteLength];
            parsed.WriteRandomness(randomness);
            for (int i = 0; i < randomness.Length; i++)
            {
                Require(randomness[i] == expectedRandomness[i], "randomness mismatch");
            }

            PikaidId lower = PikaidId.Parse("00000000000000000000000000");
            PikaidId higher = PikaidId.Parse("00000000000000000000000001");
            Require(lower.CompareTo(higher) < 0, "ordering is not ascending");
            Require(higher.CompareTo(lower) > 0, "ordering is not symmetric");

            PikaidId generated = PikaidId.New();
            Require(PikaidId.IsValid(generated.ToString()), "generated value is not valid");

            return fromBinary.ToString();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
