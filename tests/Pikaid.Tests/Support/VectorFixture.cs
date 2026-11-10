using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Pikaid.Tests;

internal sealed class VectorFixture
{
    private static readonly Lazy<VectorFixture> LazyInstance = new(VectorFixtureFile.LoadCore);

    public static VectorFixture Instance => LazyInstance.Value;

    public VectorMeta Meta { get; set; } = new();

    public List<ValidVector> Valid { get; set; } = new();

    public InvalidVectors Invalid { get; set; } = new();
}

internal sealed class VectorMeta
{
    public string Project { get; set; } = string.Empty;

    public int StringLength { get; set; }

    public int BinaryLength { get; set; }

    public string SpecRevision { get; set; } = string.Empty;
}

internal sealed class ValidVector
{
    public string Name { get; set; } = string.Empty;

    public long Timestamp { get; set; }

    public string EntropyHex { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string BinHex { get; set; } = string.Empty;

    public override string ToString() => Name;
}

internal sealed class InvalidVectors
{
    public List<InvalidStringVector> String { get; set; } = new();

    public List<InvalidBinaryVector> Binary { get; set; } = new();
}

internal sealed class InvalidStringVector
{
    public string Name { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public override string ToString() => Name;
}

internal sealed class InvalidBinaryVector
{
    public string Name { get; set; } = string.Empty;

    public string BinHex { get; set; } = string.Empty;

    public override string ToString() => Name;
}

internal static class VectorFixtureFile
{
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "pikaid-v1.json");

    internal static VectorFixture LoadCore()
    {
        string json = File.ReadAllText(Path);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement binaryMeta = document.RootElement.GetProperty("meta").GetProperty("binary");
        int binaryLength = binaryMeta.GetProperty("bytes").GetInt32();

        VectorFixture fixture = JsonSerializer.Deserialize<VectorFixture>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The conformance fixture could not be parsed.");

        fixture.Meta.BinaryLength = binaryLength;
        return fixture;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
