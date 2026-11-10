using System;

namespace Pikaid.ConsumerSmoke
{
    internal static class Program
    {
        private static void Main()
        {
            PikaidId generated = PikaidId.New();
            Require(generated.ToString().Length == PikaidId.TextLength, "unexpected text length");

            PikaidId fromText = PikaidId.Parse(generated.ToString());
            PikaidId fromBinary = PikaidId.FromBytes(generated.ToByteArray());
            Require(fromText.Equals(generated) && fromBinary.Equals(generated), "round trip mismatch");

            PikaidId fixedId = PikaidId.New(1_700_000_000UL);
            Require(fixedId.Timestamp == 1_700_000_000UL, "timestamp mismatch");
            Require(
                string.Equals(fixedId.ToString().Substring(0, 7), "0s44we8", StringComparison.Ordinal),
                "timestamp prefix mismatch");

            Console.WriteLine("net10.0 consumer ok: " + generated);
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
