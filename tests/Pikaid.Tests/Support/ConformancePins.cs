using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Pikaid.Tests;

/// <summary>
/// Pinned provenance for the conformance corpus. These values are the contract
/// between the fixture, the specification revision and the upstream PHP source;
/// they are intentionally duplicated here so an accidental fixture or provenance
/// edit fails a test instead of silently changing what conformance means.
/// </summary>
internal static class ConformancePins
{
    public const string SourceRepository = "https://github.com/pikaid/pikaid-php";

    public const string SourceTag = "v1.2.0";

    public const string SourceCommit = "e3f843edeb5157a5d2d25196cac0d69580810a95";

    public const string SourcePath = "tests/vectors/pikaid-v1.json";

    public const string SpecRevision = "1.0.4";

    public const string SpecCommit = "17014d76f568c3ac9f9ce2927c1a018fdbc36d1d";

    /// <summary>SHA-256 of the fixture exactly as stored in this repository.</summary>
    public const string FixtureSha256 = "b0c9dfdd063613424414fef5c9065fbd2f81d645d0ab42b34e27cde715613500";

    /// <summary>
    /// SHA-256 of the fixture after UTF-8 BOM removal and CRLF-to-LF normalization.
    /// Pinning both digests keeps the corpus stable across checkouts that use
    /// different line-ending settings, without weakening the byte-for-byte claim.
    /// </summary>
    public const string FixtureSha256Normalized = "b0c9dfdd063613424414fef5c9065fbd2f81d645d0ab42b34e27cde715613500";

    public static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "pikaid-v1.json");

    public static string ProvenancePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "README.md");

    public static byte[] ReadFixtureBytes() => File.ReadAllBytes(FixturePath);

    public static string ComputeSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// Returns the fixture bytes with a UTF-8 BOM removed and CRLF sequences
    /// normalized to LF, so the digest is stable regardless of checkout settings.
    /// </summary>
    public static byte[] NormalizeLineEndingsAndBom(byte[] bytes)
    {
        ReadOnlySpan<byte> content = bytes;
        if (content.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            content = content.Slice(3);
        }

        string text = Encoding.UTF8.GetString(content).Replace("\r\n", "\n", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(text);
    }

    public static string ComputeNormalizedSha256(byte[] bytes) =>
        ComputeSha256(NormalizeLineEndingsAndBom(bytes));
}
