# Changelog

All notable changes to this repository are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The pikaid identifier format is immutable and unversioned. Library releases never change
the text or binary representation of an identifier.

## [Unreleased]

### Added

- A `net8.0` package consumer that runs the packed `lib/netstandard2.1/Pikaid.dll` on the
  .NET 8 runtime and asserts the loaded assembly's `TargetFrameworkAttribute`, so the
  down-level asset is verified at run time on a down-level runtime rather than only on
  .NET 10. CI installs the .NET 8 runtime for this check.

### Changed

- CI runs on every pushed branch, not only `main`.

## [1.0.0] — 2026-11-07

First stable release, implementing pikaid specification revision 1.0.4.

> Publication status: **prepared, not yet published to NuGet.org**. The package is built
> and validated locally and in CI; publishing requires registry ownership of the
> `Pikaid` ID plus credentials that are not available to this repository's CI.

### Added

- `PikaidId`, an immutable `readonly struct` implementing `IEquatable<PikaidId>`,
  `IComparable<PikaidId>` and `IComparable`.
- Generation from the system UTC clock (`New()`, `NewBytes()`), from an explicit Unix
  timestamp, and from a `DateTimeOffset`.
- Strict parsing and conversion: `Parse`, `TryParse`, `IsValid`, `IsWellFormed`,
  `FromBytes`, `TryFromBytes`, `ToByteArray`, `WriteBytes`, `ToRandomness`,
  `WriteRandomness`, `ToString`, `Value`, `RandomnessHex`, `Timestamp`, `TimestampUtc`.
- Ordering across text, binary and struct representations, comparing the timestamp first
  and then the unsigned randomness.
- Published constants: `TextLength`, `BinaryLength`, `TimestampLength`, `RandomnessLength`,
  `TimestampByteLength`, `RandomnessByteLength`, `MaxTimestamp`, `Alphabet`.
- `PikaidFormatException` for malformed text and binary input.
- `netstandard2.1` and `net10.0` package assets, XML documentation, and portable PDBs in
  the symbols package.
- SourceLink metadata plus embedded sources in the PDBs, so debugging works while the
  repository is private.
- Official conformance vectors pinned byte-for-byte to `pikaid-php` `v1.2.0`, and an
  independent `BigInteger` Base36 oracle for property-style tests.
- CI gates for build, tests, formatting/analyzers, coverage thresholds, package contents,
  untracked and unpackaged local `.ai/` workspace content, and consumer smoke tests that
  execute both shipped assets (the `netstandard2.1` asset runs on a .NET 10 host).

### Security

- Entropy comes from `RandomNumberGenerator` (the operating system CSPRNG). CSPRNG
  failures propagate as `CryptographicException`; generation never falls back to a
  weakened source.
- Timestamps outside `0 .. 78,364,164,095` and randomness above `2^96 - 1` are rejected
  rather than truncated, clamped or wrapped.
