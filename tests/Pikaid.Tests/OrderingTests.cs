using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Ordering guarantees: ASCII-compatible textual order and unsigned binary order agree
/// with the structured comparison. Values with different seconds sort chronologically;
/// values within one second sort by randomness, not by generation order.
/// </summary>
public sealed class OrderingTests
{
    private static readonly PikaidId Minimum = PikaidId.Parse("00000000000000000000000000");
    private static readonly PikaidId Maximum = PikaidId.Parse("zzzzzzz7oiylpimjg5u2ca1ypr");

    [Fact]
    public void DifferentTimestampsSortChronologicallyEvenWithInvertedEntropy()
    {
        PikaidId earlier = TestIds.FromParts(1, "ffffffffffffffffffffffff");
        PikaidId later = TestIds.FromParts(2, "000000000000000000000000");

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(string.CompareOrdinal(earlier.ToString(), later.ToString()) < 0);
        Assert.True(earlier.ToByteArray().AsSpan().SequenceCompareTo(later.ToByteArray()) < 0);
    }

    [Fact]
    public void SameTimestampSortsByUnsignedRandomness()
    {
        PikaidId low = TestIds.FromParts(1_700_000_000, "000000000000000000000001");
        PikaidId high = TestIds.FromParts(1_700_000_000, "fffffffffffffffffffffffe");

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(string.CompareOrdinal(low.ToString(), high.ToString()) < 0);
        Assert.True(low.ToByteArray().AsSpan().SequenceCompareTo(high.ToByteArray()) < 0);
    }

    [Fact]
    public void EqualityAndComparisonAgreeAtTheBoundaries()
    {
        Assert.True(Minimum.CompareTo(Minimum) == 0);
        Assert.True(Maximum.CompareTo(Maximum) == 0);
        Assert.True(Minimum.CompareTo(Maximum) < 0);
        Assert.True(Maximum.CompareTo(Minimum) > 0);
        Assert.True(Minimum < Maximum);
        Assert.True(Minimum <= Maximum);
        Assert.True(Maximum >= Minimum);
        Assert.False(Minimum > Maximum);
        Assert.False(Maximum < Minimum);
        PikaidId equal = PikaidId.Parse("00000000000000000000000000");

        Assert.True(Minimum <= equal);
        Assert.True(Minimum >= equal);
        Assert.False(Minimum != equal);
        Assert.True(Minimum == equal);
    }

    [Fact]
    public void EqualityIsStructuralAcrossAllFields()
    {
        PikaidId reference = TestIds.FromParts(123, "00112233445566778899aabb");
        PikaidId same = PikaidId.Parse(reference.ToString());
        PikaidId differentTimestamp = TestIds.FromParts(124, "00112233445566778899aabb");
        PikaidId differentLastByte = TestIds.FromParts(123, "00112233445566778899aabc");

        Assert.Equal(reference, same);
        Assert.Equal(reference.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(reference, differentTimestamp);
        Assert.NotEqual(reference, differentLastByte);
        Assert.True(reference != differentTimestamp);
        Assert.True(reference != differentLastByte);
    }

    [Fact]
    public void NonGenericCompareToHandlesNullAndForeignTypes()
    {
        IComparable comparable = Minimum;

        Assert.Equal(1, comparable.CompareTo(null));
        Assert.True(comparable.CompareTo(Maximum) < 0);
        Assert.Throws<ArgumentException>(() => comparable.CompareTo("not a pikaid"));
        Assert.Equal(0, comparable.CompareTo(Minimum));
    }

    [Fact]
    public void SortingAgreesWithIndependentOrdinalReference()
    {
        Random random = new(20261106);
        List<PikaidId> ids = new();
        for (int i = 0; i < 250; i++)
        {
            ulong timestamp = (ulong)random.NextInt64(0, 1_000_000);
            byte[] entropy = new byte[PikaidId.RandomnessByteLength];
            random.NextBytes(entropy);
            ids.Add(TestIds.FromParts(timestamp, Hex.Format(entropy)));
        }

        List<PikaidId> actual = new(ids);
        actual.Sort();

        List<PikaidId> expected = ids
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, actual);

        for (int i = 1; i < actual.Count; i++)
        {
            Assert.True(actual[i - 1].CompareTo(actual[i]) <= 0);
            Assert.True(string.CompareOrdinal(actual[i - 1].ToString(), actual[i].ToString()) <= 0);
        }
    }

    [Fact]
    public void ComparisonSignIsIdenticalAcrossTextBinaryAndValue()
    {
        Random random = new(42);

        for (int i = 0; i < 500; i++)
        {
            PikaidId left = TestIds.FromParts(
                (ulong)random.NextInt64(0, (long)PikaidId.MaxTimestamp + 1),
                Hex.Format(RandomBytes(random)));
            PikaidId right = TestIds.FromParts(
                (ulong)random.NextInt64(0, (long)PikaidId.MaxTimestamp + 1),
                Hex.Format(RandomBytes(random)));

            int structured = Math.Sign(left.CompareTo(right));
            int text = Math.Sign(string.CompareOrdinal(left.ToString(), right.ToString()));
            int binary = Math.Sign(left.ToByteArray().AsSpan().SequenceCompareTo(right.ToByteArray()));
            int boxed = Math.Sign(((IComparable)left).CompareTo(right));
            int comparer = Math.Sign(Comparer<PikaidId>.Default.Compare(left, right));

            Assert.Equal(structured, text);
            Assert.Equal(structured, binary);
            Assert.Equal(structured, boxed);
            Assert.Equal(structured, comparer);
        }
    }

    [Fact]
    public void ComparisonIsConsistentAcrossRepeatedCalls()
    {
        PikaidId left = PikaidId.Parse("0s44we8002loz3po6mcf6vsn23");
        PikaidId right = PikaidId.Parse("0s44we8002loz3po6mcf6vsn24");

        int first = left.CompareTo(right);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(first, left.CompareTo(right));
        }
    }

    [Fact]
    public void SameTimestampComparesEachRandomnessWordInOrder()
    {
        PikaidId baseline = TestIds.FromParts(1_700_000_000, "00112233445566778899aabb");
        PikaidId secondWord = TestIds.FromParts(1_700_000_000, "00112233445566788899aabb");
        PikaidId thirdWord = TestIds.FromParts(1_700_000_000, "00112233445566778899aabc");

        // Each pair differs only in one 32-bit randomness word, so every tie-break tier
        // of CompareTo is exercised.
        AssertTiersAgree(baseline, secondWord);
        AssertTiersAgree(thirdWord, secondWord);
        AssertTiersAgree(baseline, thirdWord);
    }

    private static void AssertTiersAgree(PikaidId low, PikaidId high)
    {
        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(string.CompareOrdinal(low.ToString(), high.ToString()) < 0);
        Assert.True(low.ToByteArray().AsSpan().SequenceCompareTo(high.ToByteArray()) < 0);
    }

    [Fact]
    public void WithinOneSecondOrderIsNotGenerationOrder()
    {
        // Generation order is explicitly not a guarantee. The documented order is by
        // randomness, so two IDs generated in the same second can sort either way.
        PikaidId first = TestIds.FromParts(1_700_000_000, "ffffffffffffffffffffffff");
        PikaidId second = TestIds.FromParts(1_700_000_000, "000000000000000000000000");

        Assert.True(first.CompareTo(second) > 0);
        Assert.True(string.CompareOrdinal(first.ToString(), second.ToString()) > 0);
    }

    private static byte[] RandomBytes(Random random)
    {
        byte[] bytes = new byte[PikaidId.RandomnessByteLength];
        random.NextBytes(bytes);
        return bytes;
    }
}
