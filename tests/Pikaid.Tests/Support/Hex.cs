using System;

namespace Pikaid.Tests;

internal static class Hex
{
    public static byte[] Parse(string value) => Convert.FromHexString(value);

    public static string Format(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(value);
}
