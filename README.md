<p align="center">
  <a href="https://github.com/pikaid/pikaid-specs">
    <img src="https://raw.githubusercontent.com/pikaid/pikaid-specs/v1.0.4/logo.png" alt="pikaid" width="300">
  </a>
</p>

<p align="center">
  <strong>Small · Sortable · Secure ID generator</strong>
</p>

<div align="center">

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/language-.NET-512BD4.svg)](https://github.com/pikaid/pikaid-dotnet)
[![CI](https://github.com/pikaid/pikaid-dotnet/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/pikaid/pikaid-dotnet/actions/workflows/ci.yml)
[![Official implementation](https://img.shields.io/badge/official-implementation-2e7d32.svg)](https://github.com/pikaid/pikaid-specs)
[![Specification 1.0.4](https://img.shields.io/badge/spec-1.0.4-4b56a6.svg)](https://github.com/pikaid/pikaid-specs/tree/v1.0.4)

</div>

**pikaid** is the **official .NET implementation** of a compact, chronologically sortable, collision-resistant identifier format for databases, APIs, and distributed systems.

This is the immutable, unversioned [pikaid format](https://github.com/pikaid/pikaid-specs/tree/v1.0.4), documented in specification revision **1.0.4**.

## ✨ Why pikaid?

- **Short:** Compact, readable IDs for URLs, logs, and API resources.
- **Sortable:** Lexicographically ordered by timestamp, with **one-second precision**.
- **Collision-resistant:** 96 bits of cryptographic randomness make collisions extremely unlikely, never impossible.
- **Storage-friendly:** Use `BINARY(17)` or readable `CHAR(26)`.
- **Distributed-friendly:** No central sequence generator or coordination required.

## 🧬 Identifier format

A pikaid contains **26 lowercase Base36 characters**: a **7-character Unix-seconds timestamp** followed by **19 characters encoding 96 bits of cryptographic randomness**. Its binary form is **17 bytes**: 5 unsigned big-endian timestamp bytes followed by 12 randomness bytes.

![Identifier structure](https://raw.githubusercontent.com/pikaid/pikaid-specs/v1.0.4/structure.svg)

> **Ordering:** Different seconds sort chronologically. Within the same second, randomness—not issuance order—decides the order.

> **Security:** These are identifiers, **not secrets**, session credentials, authentication tokens, or API keys.

## ⚙️ Requirements

.NET SDK from global.json for building; supported target frameworks are documented below.

## 📦 Installation

The package has not yet been published to NuGet. Clone this repository, then build and run its included sample:

```sh
git clone https://github.com/pikaid/pikaid-dotnet.git
cd pikaid-dotnet
dotnet run --project samples/Pikaid.Sample -c Release
```

> The package is **not yet published to NuGet.org**. `1.0.0` is the prepared release
> version; the metadata below is what the package already contains.

```xml
<PackageReference Include="Pikaid" Version="1.0.0" />
```

Until then, reference the project or assembly directly:

```xml
<ProjectReference Include="path/to/pikaid-dotnet/src/Pikaid/Pikaid.csproj" />
```

The library has no runtime dependencies beyond the base class library.

## 🚀 Basic Usage

The following example works in a .NET console application's `Program.cs` after adding the local `Pikaid.csproj` project reference shown above:

```csharp
using Pikaid;

// Generate from the system UTC clock.
PikaidId id = PikaidId.New();
Console.WriteLine(id);              // e.g. 0s44we8002loz3po6mcf6vsn23 (random suffix)
Console.WriteLine(id.Timestamp);    // current Unix seconds, e.g. 1_700_000_000
Console.WriteLine(id.RandomnessHex);// 24 hex digits, e.g. 00112233445566778899aabb

// Generate with an explicit timestamp (deterministic prefix, random suffix).
PikaidId fixedTime = PikaidId.New(1_700_000_000);
PikaidId fromDate = PikaidId.New(DateTimeOffset.UtcNow);

// Binary form: exactly 17 bytes, five-byte big-endian timestamp + 12 entropy bytes.
byte[] bytes = id.ToByteArray();

// Round trips are lossless in both directions.
PikaidId fromText = PikaidId.Parse(id.ToString());
PikaidId fromBinary = PikaidId.FromBytes(bytes);
```

## 💡 Example Use Case

Use pikaid to identify **users, orders, invoices, uploads, events, and API resources**. Independent services can generate compact IDs without a shared counter, while timestamp-based ordering helps with indexing, sorting, and logs.

## 📚 Canonical specification

- [Canonical specification](https://github.com/pikaid/pikaid-specs)
- [Revision 1.0.4](https://github.com/pikaid/pikaid-specs/tree/v1.0.4)

Specification revisions describe the format; they do not change the encoded identifiers.

## 🧩 API Reference

`PikaidId` is an immutable `readonly struct` implementing `IEquatable<PikaidId>`,
`IComparable<PikaidId>`, and `IComparable`. Its `default` value is the valid all-zero
identifier `00000000000000000000000000`.

The conceptual capability map shared by all pikaid SDKs, and the .NET members that
implement each capability, is documented in
[`docs/API-PARITY.md`](https://github.com/pikaid/pikaid-dotnet/blob/main/docs/API-PARITY.md).

### Generation

| Member | Description |
| ------ | ----------- |
| `New()` | Generates an ID from the current UTC clock and the OS CSPRNG. |
| `New(ulong unixSeconds)` | Generates an ID for an explicit Unix timestamp in seconds. |
| `New(DateTimeOffset timestamp)` | Generates an ID for an explicit instant; must be a whole second. |
| `NewBytes()`, `NewBytes(ulong)` | The same, returning the 17-byte binary form. |

### Parsing and validation

| Member | Description |
| ------ | ----------- |
| `Parse(string)` / `Parse(ReadOnlySpan<char>)` | Strict parse; throws `PikaidFormatException` on invalid input. |
| `TryParse(string?, out PikaidId)` / `TryParse(ReadOnlySpan<char>, out PikaidId)` | Non-throwing parse. On failure the result is `default`. |
| `IsValid(string?)` | `true` only when the value is well-formed *and* within the 96-bit randomness range. |
| `IsWellFormed(string?)` / `IsWellFormed(ReadOnlySpan<char>)` | Shape check only: 26 ASCII characters from the alphabet. |
| `FromBytes(ReadOnlySpan<byte>)` | Strict 17-byte decode; throws `PikaidFormatException`. |
| `TryFromBytes(ReadOnlySpan<byte>, out PikaidId)` | Non-throwing binary decode. |

Validation is strict by design. Input is never trimmed, case-folded, Unicode-normalized,
or padded, and a value is never accepted "best effort". `IsWellFormed` is deliberately
weaker than `IsValid` so that text columns and shape checks can be separated from full
semantic validation:

```csharp
PikaidId.IsWellFormed("0000000zzzzzzzzzzzzzzzzzzz"); // true
PikaidId.IsValid("0000000zzzzzzzzzzzzzzzzzzz");      // false: randomness exceeds 2^96 - 1
```

### Conversion

| Member | Description |
| ------ | ----------- |
| `ToByteArray()` | Returns a new 17-byte array. |
| `WriteBytes(Span<byte>)` | Writes 17 bytes into an exactly-sized destination. |
| `ToRandomness()` | Returns the 12 randomness bytes. |
| `WriteRandomness(Span<byte>)` | Writes 12 randomness bytes into an exactly-sized destination. |
| `ToString()` / `Value` | The canonical 26-character text. |
| `RandomnessHex` | The 12 randomness bytes as 24 lowercase hex characters. |
| `Timestamp` | Unix seconds as `ulong`. |
| `TimestampUtc` | The timestamp as a `DateTimeOffset` in UTC. |

### Comparison

`CompareTo`, `==`, `!=`, `<`, `>`, `<=`, `>=` compare by timestamp and then by unsigned
randomness. This is equivalent to ordinal comparison of the text form and unsigned
bytewise comparison of the binary form, so a single ordering is available in every
representation. `GetHashCode` is consistent with equality.

### Constants

`TextLength` (26), `BinaryLength` (17), `TimestampLength` (7), `RandomnessLength` (19),
`TimestampByteLength` (5), `RandomnessByteLength` (12), `MaxTimestamp`, and `Alphabet`
(`0123456789abcdefghijklmnopqrstuvwxyz`).

### Exceptions

| Situation | Exception |
| --------- | --------- |
| Invalid text or binary value (`Parse`, `FromBytes`) | `PikaidFormatException` (derives from `FormatException`) |
| `null` string passed to `Parse` | `ArgumentNullException` |
| Timestamp outside `0 .. MaxTimestamp` | `ArgumentOutOfRangeException` |
| `DateTimeOffset` with sub-second precision | `ArgumentException` |
| `WriteBytes` / `WriteRandomness` destination of the wrong length | `ArgumentException` |
| System clock outside the supported range | `InvalidOperationException` |
| OS CSPRNG failure | `CryptographicException` (propagated, never silently replaced) |

Generation fails closed: if the clock or the CSPRNG is unusable, no ID is produced with
weakened randomness or a truncated timestamp.

## 🔄 Order Guarantee

- Values with **different timestamps** sort chronologically by construction.
- Values generated **within the same second** sort by randomness, **not** by generation
  order. There is no monotonic counter, and none is implied.
- Timestamp resolution is **one second**; ordering granularity is therefore one second.
- Sorting is only correct with an ordering that matches byte order. For text columns use
  `CHAR(26)`/`VARCHAR(26)` with a binary or otherwise verified ASCII-compatible collation;
  many database default collations are case-insensitive and would sort incorrectly. For
  binary columns use `BINARY(17)` or an equivalent fixed-width binary type compared
  bytewise. Database schema and index choices remain the caller's responsibility.

## 🛢 Storage Recommendations

| Representation | Size | Recommended for |
| --- | ---: | --- |
| **BINARY(17)** | 17 bytes | Compact primary keys and indexes |
| **CHAR(26)** | 26 characters | Readable SQL, debugging, URLs, and logs |

Use an **ASCII bytewise/binary collation** for text columns (for example, `ascii_bin` on MariaDB/MySQL). Case-insensitive or locale-aware sorting is not a substitute for canonical bytewise comparison.

## ✅ Verification and release status

The [CI workflow](https://github.com/pikaid/pikaid-dotnet/actions/workflows/ci.yml) runs native tests, pinned automated conformance checks, and language-specific quality gates. To run the native checks locally from a checkout: `dotnet test Pikaid.sln -c Release`.

**Automated passing tests are not a security certification.** The independent CSPRNG source and failure-path audits (`P-GENERATE-RNG-SOURCE` and `P-GENERATE-RNG-FAILURE`) still require separate human evidence.

<details>
<summary>Platform, tooling, and conformance details</summary>

| TFM | Why |
| --- | --- |
| `netstandard2.1` | Compile-time compatibility with `netstandard2.1` consumers (.NET Core 3.x, .NET 5+, Mono, Unity and others) with `Span<T>` available and no extra packages. |
| `net10.0` | Current LTS, with modern analyzer and API coverage. |

The test suite and sample target `net10.0`. Both library targets compile from the same
sources and behave identically; the only conditional compilation is a one-line
`ArgumentNullException.ThrowIfNull` shim, which does not exist on `netstandard2.1`.

The `netstandard2.1` row describes compile-time API compatibility plus the runtime
evidence this repository actually executes. A clean `netstandard2.1` consumer compiles
against the packed asset, the shipped `lib/netstandard2.1/Pikaid.dll` is executed on a
.NET 10 host (referenced straight out of the `.nupkg`), and a clean `net8.0` consumer
executes that same shipped asset on the .NET 8 runtime. The `net8.0` consumer asserts
that the loaded assembly's `TargetFrameworkAttribute` is
`.NETStandard,Version=v2.1`, so the down-level asset is proven to run, not only to
compile. No wider down-level runtime matrix is claimed here; run your own target runtime
if you need runtime verification for it.

`Pikaid.dll` is deterministic and ships with a portable PDB in the companion
`Pikaid.1.0.0.snupkg` symbols package. The PDBs carry both SourceLink metadata and the
full C# sources embedded.

SourceLink points to the public source at
`https://raw.githubusercontent.com/pikaid/pikaid-dotnet/<commit>/*`.
Sources are also embedded in the portable PDB, so stepping into the library
works offline or when source downloading is unavailable.

Requires the .NET SDK pinned in [`global.json`](global.json) (10.0.4xx feature band).

```bash
dotnet restore Pikaid.sln
dotnet build Pikaid.sln -c Release

# Build clean, warnings are errors, code style is enforced in the build.
dotnet format Pikaid.sln --verify-no-changes

dotnet test Pikaid.sln -c Release
dotnet test Pikaid.sln -c Release --collect:"XPlat Code Coverage"

# End-to-end tour of the public API.
dotnet run --project samples/Pikaid.Sample -c Release

# Package (not published by this repository's CI).
dotnet pack src/Pikaid/Pikaid.csproj -c Release -o artifacts
```

The test project covers the official conformance vectors, independent known-answer
values, strict validation, generation seams, lossless conversions, ordering equivalence
across all three representations, the public API surface, and seeded property-style
comparisons against an independent `BigInteger` oracle.

CI runs the suite on Linux, macOS and Windows, enforces line and branch coverage
thresholds, asserts that no local `.ai/` workspace content is tracked or packaged, and
restores the packed package into clean `net10.0`, `net8.0` and `netstandard2.1` consumer
projects. The `netstandard2.1` asset is unpacked from the `.nupkg` and executed on the
.NET 10 host, and the `net8.0` consumer executes the same shipped asset on the .NET 8
runtime, so the shipped assembly is exercised at run time rather than only compiled
against. A separate
`conformance` job pins the official cross-language suite commit, verifies its checksums,
manifest and schemas, runs its self-tests and Node.js oracle, then executes the adapter
and a deliberately corrupted negative control that must fail.

This library is also validated against the official cross-language conformance suite in
[`pikaid/pikaid-specs`](https://github.com/pikaid/pikaid-specs) (package revision `1.0.0`,
status `OFFICIAL`, spec revision `1.0.4`), pinned at the immutable commit
[`059780cc`](https://github.com/pikaid/pikaid-specs/tree/059780cc0e5b113443812720d95c630649bf3aa3/conformance).
The suite ships `SHA256SUMS`, JSON Schemas, a fixture integrity verifier, its own
self-tests and an independent Node.js `BigInt` oracle. CI fetches that exact commit,
verifies all 31 checksummed files and the manifest, and runs every applicable row through
a small **test-only** JSON-Lines adapter (`tests/Pikaid.ConformanceAdapter`) that calls
the real public API. The adapter is never packed and is not referenced by the library.

```bash
# Inside a checkout of the pinned suite commit, under conformance/:
python tools/verify_vectors.py --expect-status OFFICIAL \
  --expect-package-revision 1.0.0 --expect-spec-revision 1.0.4
python -m unittest discover -s tools/tests -v
node tools/verify_oracle_node.mjs

# Runs all automated cases and exits 0.
python tools/run_adapter.py --automated-only -- \
  dotnet path/to/Pikaid.ConformanceAdapter.dll
```

`--automated-only` exit `0` means the automated checks passed. It is **not** a
conformance certification: the suite defines two manual CSPRNG review obligations,
`P-GENERATE-RNG-SOURCE` and `P-GENERATE-RNG-FAILURE`, that require independent human
review. Running the runner without `--automated-only` therefore exits `3` with
`AUTOMATED_PASS_AUDIT_REQUIRED` while those audits are outstanding. This repository does
not claim that certification.

</details>

Registry publication is not implied by a green CI: use the installation method documented above.

## 🤝 Contributing

Contributions are always welcome! 💙

Whether you're fixing a typo, improving documentation, adding tests, or making pikaid even better, every contribution is appreciated.

**Want to help? Here's how:**

- 🐛 **Found a bug?** [Open an issue](https://github.com/pikaid/pikaid-dotnet/issues) with clear reproduction steps.
- 💡 **Have an idea?** Start with an issue before making major changes.
- 🛠️ **Ready to contribute?** Open a pull request explaining your changes.
- 🧪 **Keep it tested.** Add meaningful regression tests for behavior changes and follow the existing conventions.
- ✅ **Make sure CI passes.** All required checks must be green before a pull request is merged. No shortcuts!

**One important rule:** pikaid's identifier format is immutable. Contributions must preserve the [canonical specification (revision 1.0.4)](https://github.com/pikaid/pikaid-specs/tree/v1.0.4).

🔐 **Found a security vulnerability?** Report it privately using GitHub's security reporting (when available), or [contact the maintainer](mailto:larananas.dev@proton.me)—not through a public issue.

**Every improvement matters, big or small. Thanks for helping make pikaid better!** 🐇

## 📄 License

The implementation is available under the [MIT License](LICENSE).

## ✉️ Contact

For licensing or project-related questions: [larananas.dev@proton.me](mailto:larananas.dev@proton.me).

