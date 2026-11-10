using System;
using System.Reflection;
using System.Runtime.Versioning;

namespace Pikaid.ConsumerSmoke
{
    /// <summary>
    /// Executes the <c>netstandard2.1</c> asset unpacked from the packed nupkg on a
    /// .NET 10 runtime. Compile-only verification cannot prove that the shipped
    /// <c>lib/netstandard2.1/Pikaid.dll</c> actually loads and behaves correctly.
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            string? frameworkName = typeof(PikaidId).Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            Require(
                string.Equals(frameworkName, ".NETStandard,Version=v2.1", StringComparison.Ordinal),
                "expected the netstandard2.1 asset, loaded: " + (frameworkName ?? "<none>"));

            const string vector = "0s44we8002loz3po6mcf6vsn23";
            string roundTripped = NetStandard21Smoke.RoundTrip(vector);
            Require(
                string.Equals(roundTripped, vector, StringComparison.Ordinal),
                "netstandard2.1 round trip mismatch: " + roundTripped);

            Console.WriteLine("netstandard2.1 asset executed: " + roundTripped);
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
