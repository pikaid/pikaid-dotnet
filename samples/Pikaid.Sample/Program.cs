using System;
using System.Collections.Generic;
using Pikaid;

// A small end-to-end tour of the public API. Run with:
//   dotnet run --project samples/Pikaid.Sample

PikaidId id = PikaidId.New();

Console.WriteLine($"pikaid           {id}");
Console.WriteLine($"value            {id.Value}");
Console.WriteLine($"timestamp        {id.Timestamp}");
Console.WriteLine($"timestamp (UTC)  {id.TimestampUtc:O}");
Console.WriteLine($"randomness       {id.RandomnessHex}");

byte[] binary = id.ToByteArray();
Console.WriteLine($"binary           {Convert.ToHexStringLower(binary)} ({binary.Length} bytes)");

PikaidId fromText = PikaidId.Parse(id.ToString());
PikaidId fromBinary = PikaidId.FromBytes(binary);

Console.WriteLine($"text round trip  {fromText == id}");
Console.WriteLine($"binary roundtrip {fromBinary == id}");

// A fixed timestamp makes the timestamp prefix reproducible.
PikaidId fixedTimestamp = PikaidId.New(1_700_000_000);
Console.WriteLine();
Console.WriteLine($"fixed ts         {fixedTimestamp} (prefix {fixedTimestamp.ToString()[..7]})");

// Explicit binary construction, e.g. when reading a database BINARY(17) column.
PikaidId known = PikaidId.FromBytes(Convert.FromHexString("006553f10000112233445566778899aabb"));
Console.WriteLine($"known            {known}");
Console.WriteLine($"known matches    {known.ToString() == "0s44we8002loz3po6mcf6vsn23"}");

Console.WriteLine();
Console.WriteLine($"valid            {PikaidId.IsValid("0s44we8002loz3po6mcf6vsn23")}");
Console.WriteLine($"invalid          {PikaidId.IsValid("0S44WE8002LOZ3PO6MCF6VSN23")}");
Console.WriteLine($"well formed only {PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzzz")}");
Console.WriteLine($"fully valid      {PikaidId.IsValid("0000000zzzzzzzzzzzzzzzzzzz")}");

// Sorting is chronological for different timestamps; within one second it is by
// randomness, not by generation order.
List<PikaidId> ids = new()
{
    PikaidId.New(1_700_000_002),
    PikaidId.New(1_700_000_000),
    PikaidId.New(1_700_000_001),
};

ids.Sort();
Console.WriteLine();
Console.WriteLine("sorted:");
foreach (PikaidId value in ids)
{
    Console.WriteLine($"  {value}  {value.TimestampUtc:O}");
}

// Invalid text is reported, never normalized or truncated.
if (!PikaidId.TryParse("not-a-pikaid", out PikaidId parsed))
{
    Console.WriteLine();
    Console.WriteLine($"rejected         'not-a-pikaid' (got default: {parsed == default})");
}

try
{
    _ = PikaidId.Parse("NOT-A-VALID-PIKAID-VALUE");
}
catch (PikaidFormatException exception)
{
    Console.WriteLine($"strict parse     {exception.GetType().Name}");
}
