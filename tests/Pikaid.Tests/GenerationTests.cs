using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Generation behavior: cryptographic entropy, explicit timestamps, fail-closed errors.
/// </summary>
public sealed class GenerationTests
{
    private static readonly byte[] FixedEntropy =
    {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B,
    };

    [Fact]
    public void NewUsesCurrentUtcTimestampWithinTheObservedWindow()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PikaidId id = PikaidId.New();
        long after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.InRange((long)id.Timestamp, before, after);
        Assert.Equal(id.Timestamp, (ulong)id.TimestampUtc.ToUnixTimeSeconds());
        Assert.Equal(id, PikaidId.Parse(id.ToString()));
        Assert.Equal(PikaidId.BinaryLength, id.ToByteArray().Length);
    }

    [Fact]
    public void NewProducesIndependentRandomnessAcrossCalls()
    {
        const int count = 64;
        HashSet<string> values = new(StringComparer.Ordinal);

        for (int i = 0; i < count; i++)
        {
            values.Add(PikaidId.New().RandomnessHex);
        }

        // A 96-bit CSPRNG repeating within 64 draws is not a realistic expectation;
        // this checks independence without claiming guaranteed uniqueness.
        Assert.Equal(count, values.Count);
    }

    [Fact]
    public void NewRequestsExactlyTwelveEntropyBytes()
    {
        CountingEntropySource source = new();

        using (new EntropyScope(source))
        {
            PikaidId.New();
            PikaidId.New(123);
            _ = PikaidId.NewBytes();
            _ = PikaidId.NewBytes(123);
        }

        Assert.Equal(4, source.Calls);
        Assert.Equal(4 * PikaidId.RandomnessByteLength, source.TotalBytes);
    }

    [Fact]
    public void GenerationUsesTheConfiguredEntropyBytesExactly()
    {
        PikaidId expected = TestIds.FromParts(42, "000102030405060708090a0b");

        using (new EntropyScope(new FixedEntropySource(FixedEntropy)))
        {
            PikaidId id = PikaidId.New(42);

            Assert.Equal(expected, id);
            Assert.Equal(expected.ToString(), id.ToString());
            Assert.Equal("000102030405060708090a0b", id.RandomnessHex);
        }
    }

    [Fact]
    public void NewWithExplicitTimestampUsesThatTimestamp()
    {
        using (new EntropyScope(new FixedEntropySource(FixedEntropy)))
        {
            PikaidId id = PikaidId.New(1_700_000_000UL);

            Assert.Equal(1_700_000_000UL, id.Timestamp);
            Assert.Equal(PikaidId.TextLength, id.ToString().Length);
            Assert.Equal("0s44we8", id.ToString().Substring(0, 7));
        }
    }

    [Fact]
    public void NewBytesReturnsTheBinaryFormOfTheGeneratedValue()
    {
        using (new EntropyScope(new FixedEntropySource(FixedEntropy)))
        {
            byte[] bytes = PikaidId.NewBytes(42);
            PikaidId id = PikaidId.FromBytes(bytes);

            Assert.Equal(PikaidId.BinaryLength, bytes.Length);
            Assert.Equal(42UL, id.Timestamp);
            Assert.Equal(TestIds.FromParts(42, "000102030405060708090a0b"), id);
        }
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(1_700_000_000UL)]
    [InlineData(78_364_164_095UL)]
    public void NewAcceptsEveryValidBoundaryTimestamp(ulong timestamp)
    {
        PikaidId id = PikaidId.New(timestamp);

        Assert.Equal(timestamp, id.Timestamp);
        Assert.True(PikaidId.IsValid(id.ToString()));
        Assert.Equal(26, id.ToString().Length);
    }

    [Theory]
    [InlineData(78_364_164_096UL)]
    [InlineData(ulong.MaxValue)]
    public void NewRejectsTimestampsAboveTheMaximum(ulong timestamp)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PikaidId.New(timestamp));

        Assert.Equal("unixSeconds", exception.ParamName);
    }

    [Fact]
    public void NewAcceptsTheMaximumTimestampThroughDateTimeOffset()
    {
        DateTimeOffset maximum = DateTimeOffset.FromUnixTimeSeconds((long)PikaidId.MaxTimestamp);

        PikaidId id = PikaidId.New(maximum);

        Assert.Equal(PikaidId.MaxTimestamp, id.Timestamp);
        Assert.StartsWith("zzzzzzz", id.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NewConvertsDateTimeOffsetToUtcSeconds()
    {
        DateTimeOffset withOffset = DateTimeOffset
            .FromUnixTimeSeconds(1_700_000_000)
            .ToOffset(TimeSpan.FromHours(2));

        PikaidId id = PikaidId.New(withOffset);

        Assert.Equal(1_700_000_000UL, id.Timestamp);
        Assert.Equal(1_700_000_000UL, (ulong)PikaidId.New(withOffset.ToUniversalTime()).Timestamp);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(999)]
    public void NewRejectsSubSecondDateTimeOffsetsInsteadOfTruncating(int milliseconds)
    {
        DateTimeOffset value = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddMilliseconds(milliseconds);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => PikaidId.New(value));

        Assert.Equal("timestamp", exception.ParamName);
        Assert.Contains("whole number of seconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NewRejectsDateTimeOffsetsBeforeTheUnixEpoch()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PikaidId.New(DateTimeOffset.FromUnixTimeSeconds(-1)));

        Assert.Equal("timestamp", exception.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(() => PikaidId.New(DateTimeOffset.MinValue));
    }

    [Fact]
    public void NewRejectsDateTimeOffsetsBeyondTheMaximum()
    {
        DateTimeOffset beyond = DateTimeOffset.FromUnixTimeSeconds((long)PikaidId.MaxTimestamp + 1);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PikaidId.New(beyond));

        Assert.Equal("timestamp", exception.ParamName);

        // DateTimeOffset.MaxValue has sub-second precision, so trim it first to exercise
        // the range check rather than the sub-second check.
        DateTimeOffset maximum = DateTimeOffset.MaxValue;
        maximum = maximum.AddTicks(-(maximum.Ticks % TimeSpan.TicksPerSecond));

        Assert.Throws<ArgumentOutOfRangeException>(() => PikaidId.New(maximum));
    }

    [Fact]
    public void CryptographicFailureFailsClosedWithoutRetrying()
    {
        ThrowingEntropySource source = new();

        using (new EntropyScope(source))
        {
            Assert.Throws<CryptographicException>(() => PikaidId.New());
            Assert.Throws<CryptographicException>(() => PikaidId.New(1));
            Assert.Throws<CryptographicException>(() => PikaidId.NewBytes());
            Assert.Throws<CryptographicException>(() => PikaidId.NewBytes(1));
            Assert.Throws<CryptographicException>(() => PikaidId.New(DateTimeOffset.UnixEpoch));
        }

        Assert.Equal(5, source.Calls);
    }

    [Fact]
    public void EntropySourceCannotBeNull()
    {
        IEntropySource previous = PikaidEntropy.Source;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => PikaidEntropy.Source = null!);

        Assert.Equal("value", exception.ParamName);
        Assert.Same(previous, PikaidEntropy.Source);
    }

    [Fact]
    public void InvalidTimestampsDoNotConsumeEntropy()
    {
        CountingEntropySource source = new();

        using (new EntropyScope(source))
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PikaidId.New(PikaidId.MaxTimestamp + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PikaidId.New(ulong.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PikaidId.NewBytes(PikaidId.MaxTimestamp + 1));
            Assert.Throws<ArgumentException>(
                () => PikaidId.New(DateTimeOffset.UnixEpoch.AddMilliseconds(1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PikaidId.New(DateTimeOffset.FromUnixTimeSeconds(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PikaidId.New(DateTimeOffset.FromUnixTimeSeconds((long)PikaidId.MaxTimestamp + 1)));
        }

        Assert.Equal(0, source.Calls);
        Assert.Equal(0, source.TotalBytes);
    }

    [Fact]
    public void ClockRangeFailuresDoNotConsumeEntropy()
    {
        CountingEntropySource source = new();

        using (new EntropyScope(source))
        using (new ClockScope(() => -1))
        {
            Assert.Throws<InvalidOperationException>(() => PikaidId.New());
        }

        using (new EntropyScope(source))
        using (new ClockScope(() => (long)PikaidId.MaxTimestamp + 1))
        {
            Assert.Throws<InvalidOperationException>(() => PikaidId.NewBytes());
        }

        Assert.Equal(0, source.Calls);
        Assert.Equal(0, source.TotalBytes);
    }

    [Fact]
    public void EntropySourceIsRestoredToTheOperatingSystemCspAfterAFailureScope()
    {
        using (new EntropyScope(new ThrowingEntropySource()))
        {
            Assert.Throws<CryptographicException>(() => PikaidId.New(0));
        }

        Assert.Same(CryptoEntropySource.Instance, PikaidEntropy.Source);

        PikaidId id = PikaidId.New(0);
        Assert.True(PikaidId.IsValid(id.ToString()));
    }

    [Fact]
    public void ClockProviderCannotBeNull()
    {
        Func<long> previous = PikaidClock.UnixSecondsProvider;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => PikaidClock.UnixSecondsProvider = null!);

        Assert.Equal("value", exception.ParamName);
        Assert.Same(previous, PikaidClock.UnixSecondsProvider);
    }

    [Fact]
    public void ConcurrentGenerationProducesValidIndependentIdentifiers()
    {
        const int threads = 8;
        const int perThread = 64;
        string[] values = new string[threads * perThread];

        using ManualResetEventSlim start = new(false);
        Thread[] workers = new Thread[threads];
        for (int thread = 0; thread < threads; thread++)
        {
            int offset = thread * perThread;
            workers[thread] = new Thread(() =>
            {
                start.Wait();
                for (int i = 0; i < perThread; i++)
                {
                    values[offset + i] = PikaidId.New().ToString();
                }
            });

            workers[thread].Start();
        }

        start.Set();
        foreach (Thread worker in workers)
        {
            worker.Join();
        }

        HashSet<string> distinct = new(values, StringComparer.Ordinal);
        Assert.Equal(values.Length, distinct.Count);

        foreach (string value in values)
        {
            Assert.True(PikaidId.IsValid(value));
            Assert.Equal(PikaidId.TextLength, value.Length);
        }
    }

    [Fact]
    public void ClockBeforeEpochThrowsInvalidOperation()
    {
        using (new ClockScope(() => -1))
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PikaidId.New());
            Assert.Contains("before the Unix epoch", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ClockBeyondMaximumThrowsInvalidOperation()
    {
        using (new ClockScope(() => (long)PikaidId.MaxTimestamp + 1))
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PikaidId.New());
            Assert.Contains("beyond", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ClockAtMaximumIsAccepted()
    {
        using (new ClockScope(() => (long)PikaidId.MaxTimestamp))
        {
            PikaidId id = PikaidId.New();

            Assert.Equal(PikaidId.MaxTimestamp, id.Timestamp);
        }
    }

    [Fact]
    public void DefaultEntropySourceIsTheOperatingSystemCsp()
    {
        Assert.Same(CryptoEntropySource.Instance, PikaidEntropy.Source);
    }

    [Fact]
    public void CryptoEntropySourceWritesRequestedBytes()
    {
        Span<byte> first = stackalloc byte[PikaidId.RandomnessByteLength];
        Span<byte> second = stackalloc byte[PikaidId.RandomnessByteLength];

        CryptoEntropySource.Instance.Fill(first);
        CryptoEntropySource.Instance.Fill(second);

        Assert.Equal(PikaidId.RandomnessByteLength, first.Length);
        Assert.False(first.SequenceEqual(second));
    }

    [Fact]
    public void CryptoEntropySourceAcceptsEmptyDestinations()
    {
        CryptoEntropySource.Instance.Fill(Span<byte>.Empty);
    }

    [Fact]
    public void RealGenerationProducesStructurallyValidIdentifiers()
    {
        for (int i = 0; i < 100; i++)
        {
            PikaidId id = PikaidId.New();

            Assert.True(PikaidId.IsValid(id.ToString()));
            Assert.True(PikaidId.TryParse(id.ToString(), out PikaidId reparsed));
            Assert.Equal(id, reparsed);
            Assert.Equal(id, PikaidId.FromBytes(id.ToByteArray()));
            Assert.InRange(id.Timestamp, 0UL, PikaidId.MaxTimestamp);
        }
    }

    private sealed class EntropyScope : IDisposable
    {
        private readonly IEntropySource _previous;

        public EntropyScope(IEntropySource source)
        {
            _previous = PikaidEntropy.Source;
            PikaidEntropy.Source = source;
        }

        public void Dispose() => PikaidEntropy.Source = _previous;
    }

    private sealed class ClockScope : IDisposable
    {
        private readonly Func<long> _previous;

        public ClockScope(Func<long> provider)
        {
            _previous = PikaidClock.UnixSecondsProvider;
            PikaidClock.UnixSecondsProvider = provider;
        }

        public void Dispose() => PikaidClock.UnixSecondsProvider = _previous;
    }
}
