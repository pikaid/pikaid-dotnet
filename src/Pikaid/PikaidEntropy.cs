using System;
using System.Security.Cryptography;

namespace Pikaid;

/// <summary>
/// Abstraction over the cryptographic random source used for ID generation.
/// Exists so tests can exercise fail-closed behavior without weakening the
/// default production path, which always uses the operating system CSPRNG.
/// </summary>
internal interface IEntropySource
{
    void Fill(Span<byte> destination);
}

/// <summary>
/// The production entropy source: <see cref="RandomNumberGenerator"/> backed by
/// the operating system CSPRNG.
/// </summary>
internal sealed class CryptoEntropySource : IEntropySource
{
    internal static readonly CryptoEntropySource Instance = new();

    private readonly object _gate = new();
    private readonly RandomNumberGenerator _generator = RandomNumberGenerator.Create();

    private CryptoEntropySource()
    {
    }

    public void Fill(Span<byte> destination)
    {
        if (destination.IsEmpty)
        {
            return;
        }

        byte[] buffer = new byte[destination.Length];

        // RandomNumberGenerator instances are not documented as thread-safe.
        lock (_gate)
        {
            _generator.GetBytes(buffer);
        }

        buffer.CopyTo(destination);
    }
}

internal static class PikaidEntropy
{
    // Tests may swap in a deterministic or failing source. Marking the field volatile
    // keeps the publication of that swap visible to every thread that generates IDs.
    private static volatile IEntropySource _source = CryptoEntropySource.Instance;

    internal static IEntropySource Source
    {
        get => _source;
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static void Fill(Span<byte> destination) => _source.Fill(destination);
}
