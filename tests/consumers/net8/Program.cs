using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pikaid.ConsumerSmoke
{
    internal static class Program
    {
        private static void Main()
        {
            // A net8.0 application cannot reference lib/net10.0. NuGet therefore binds
            // lib/netstandard2.1, and the assembly's target framework attribute proves
            // which shipped asset was loaded at run time.
            string? targetFramework = typeof(PikaidId).Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;
            Require(
                string.Equals(targetFramework, ".NETStandard,Version=v2.1", StringComparison.Ordinal),
                "expected the netstandard2.1 asset, loaded " + (targetFramework ?? "<none>"));

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

            Console.WriteLine(
                "net8.0 consumer ok on " + RuntimeInformation.FrameworkDescription
                + " with " + targetFramework + ": " + generated);
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
