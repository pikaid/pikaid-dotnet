using System;
using System.Security.Cryptography;

namespace Pikaid.Tests;

internal sealed class FixedEntropySource : IEntropySource
{
    private readonly byte[] _value;

    public FixedEntropySource(params byte[] value) => _value = value;

    public int Calls { get; private set; }

    public int LastRequestedLength { get; private set; }

    public void Fill(Span<byte> destination)
    {
        Calls++;
        LastRequestedLength = destination.Length;
        _value.AsSpan(0, destination.Length).CopyTo(destination);
    }
}

internal sealed class CountingEntropySource : IEntropySource
{
    public int Calls { get; private set; }

    public int TotalBytes { get; private set; }

    public void Fill(Span<byte> destination)
    {
        Calls++;
        TotalBytes += destination.Length;
        destination.Clear();
    }
}

internal sealed class ThrowingEntropySource : IEntropySource
{
    public int Calls { get; private set; }

    public void Fill(Span<byte> destination)
    {
        Calls++;
        throw new CryptographicException("Simulated cryptographic random source failure.");
    }
}
