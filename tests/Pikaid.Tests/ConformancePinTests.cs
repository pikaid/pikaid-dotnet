using System;
using System.IO;
using System.Text;
using Xunit;

namespace Pikaid.Tests;

/// <summary>
/// Guards the provenance contract of the conformance corpus: the fixture digest,
/// the upstream source pin and the specification revision must not drift silently.
/// </summary>
public sealed class ConformancePinTests
{
    [Fact]
    public void FixtureBytesMatchThePinnedDigest()
    {
        byte[] bytes = ConformancePins.ReadFixtureBytes();

        Assert.Equal(ConformancePins.FixtureSha256, ConformancePins.ComputeSha256(bytes));
        Assert.Equal(
            ConformancePins.FixtureSha256Normalized,
            ConformancePins.ComputeNormalizedSha256(bytes));
    }

    [Fact]
    public void FixtureDigestIsStableAcrossLineEndingsAndBom()
    {
        byte[] canonical = ConformancePins.ReadFixtureBytes();
        string text = Encoding.UTF8.GetString(ConformancePins.NormalizeLineEndingsAndBom(canonical));

        byte[] crlf = Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal));
        byte[] bom = { 0xEF, 0xBB, 0xBF };
        byte[] withBom = new byte[bom.Length + crlf.Length];
        bom.CopyTo(withBom, 0);
        crlf.CopyTo(withBom, bom.Length);

        Assert.Equal(ConformancePins.FixtureSha256Normalized, ConformancePins.ComputeNormalizedSha256(crlf));
        Assert.Equal(ConformancePins.FixtureSha256Normalized, ConformancePins.ComputeNormalizedSha256(withBom));
        Assert.NotEqual(ConformancePins.FixtureSha256, ConformancePins.ComputeSha256(crlf));
    }

    [Fact]
    public void ProvenanceDocumentPinsTheSameSourceAndSpecification()
    {
        string provenance = File.ReadAllText(ConformancePins.ProvenancePath);

        Assert.Contains(ConformancePins.SourceRepository, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.SourceTag, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.SourceCommit, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.SourcePath, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.FixtureSha256, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.SpecRevision, provenance, StringComparison.Ordinal);
        Assert.Contains(ConformancePins.SpecCommit, provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void PinnedIdentifiersAreTheReviewedValues()
    {
        Assert.Equal("v1.2.0", ConformancePins.SourceTag);
        Assert.Equal("e3f843edeb5157a5d2d25196cac0d69580810a95", ConformancePins.SourceCommit);
        Assert.Equal("1.0.4", ConformancePins.SpecRevision);
        Assert.Equal("17014d76f568c3ac9f9ce2927c1a018fdbc36d1d", ConformancePins.SpecCommit);
        Assert.Equal(
            "b0c9dfdd063613424414fef5c9065fbd2f81d645d0ab42b34e27cde715613500",
            ConformancePins.FixtureSha256);
    }
}
