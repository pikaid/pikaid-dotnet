# Public API parity map

pikaid is an immutable, unversioned identifier format. Every conforming SDK exposes
the same concepts; the concrete member names differ by language. This document maps
each conceptual capability of the format to its public .NET surface so that
cross-language reviewers can check parity without reading source, and so that a future
release cannot silently drop a capability under a cosmetic rename.

The format itself is defined by pikaid specification revision 1.0.4
(commit `17014d76f568c3ac9f9ce2927c1a018fdbc36d1d`). These names are .NET idioms; they
are not required by the specification and are not a cross-language ABI.

## Capability map

| Concept | .NET member(s) | Kind | Notes |
| ------- | -------------- | ---- | ----- |
| Generate now | `PikaidId.New()`, `NewBytes()` | factory | Uses the system UTC clock and the OS CSPRNG. |
| Generate at a time (Unix seconds) | `New(ulong unixSeconds)`, `NewBytes(ulong)` | factory | Rejects values above `MaxTimestamp`; consumes no entropy on failure. |
| Generate at a time (instant) | `New(DateTimeOffset timestamp)` | factory | Requires a whole second and a UTC-representable instant in range. |
| Strict text parse | `Parse(string)`, `Parse(ReadOnlySpan<char>)` | factory | Throws `PikaidFormatException`; never coerces, trims, pads or case-folds. |
| Non-throwing text parse | `TryParse(string?, out PikaidId)`, `TryParse(ReadOnlySpan<char>, out PikaidId)` | factory | Returns `false` and `default` on any failure. |
| Full validity | `IsValid(string?)` | predicate | Shape **and** the 96-bit randomness limit. |
| Shape-only check | `IsWellFormed(string?)`, `IsWellFormed(ReadOnlySpan<char>)` | predicate | Exactly 26 lowercase Base36 characters; does **not** check the randomness limit. |
| Strict binary decode | `FromBytes(ReadOnlySpan<byte>)` | factory | Exactly 17 bytes; throws `PikaidFormatException`. |
| Non-throwing binary decode | `TryFromBytes(ReadOnlySpan<byte>, out PikaidId)` | factory | Returns `false` and `default` on any failure. |
| Text → binary | `ToByteArray()`, `WriteBytes(Span<byte>)` | instance | 5-byte big-endian timestamp + 12 randomness bytes. |
| Binary → text | `Parse` / `FromBytes` then `ToString()` / `Value` | instance | Lossless in both directions, including leading zeros. |
| Timestamp (Unix seconds) | `Timestamp` | property | `ulong`, `0 .. 78,364,164,095`. |
| Timestamp (UTC instant) | `TimestampUtc` | property | `DateTimeOffset` with a zero offset, one-second resolution. |
| Randomness (bytes) | `ToRandomness()`, `WriteRandomness(Span<byte>)` | instance | 12 bytes in original order; copies are independent of the struct. |
| Randomness (text) | `RandomnessHex` | property | 24 lowercase hex characters. |
| Canonical text | `ToString()`, `Value` | instance | Exactly 26 lowercase Base36 characters. |
| Equality | `Equals(PikaidId)`, `Equals(object?)`, `==`, `!=` | instance / operator | Timestamp and all 96 randomness bits. |
| Hashing | `GetHashCode()` | instance | Consistent with equality; safe as a dictionary key. |
| Ordering | `CompareTo(PikaidId)`, `IComparable.CompareTo(object?)` (explicit), `<`, `>`, `<=`, `>=` | instance / operator | Timestamp first, then unsigned randomness; matches ordinal text and unsigned binary order. |
| Error propagation | `PikaidFormatException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, `ArgumentException`, `InvalidOperationException`, `CryptographicException` | exception | See the README exception table; generation fails closed. |
| Format constants | `TextLength`, `BinaryLength`, `TimestampLength`, `RandomnessLength`, `TimestampByteLength`, `RandomnessByteLength`, `MaxTimestamp`, `Alphabet` | constants | They describe the immutable format, not a library version. |

## Invariants that all entrypoints share

- **One grammar.** `Parse`, `TryParse`, `IsValid` and `IsWellFormed` accept the same
  lowercase Base36 alphabet. There is no culture-sensitive or Unicode-normalized path.
- **One binary layout.** `ToByteArray`, `WriteBytes` and `FromBytes` use exactly
  `5 + 12` bytes: an unsigned big-endian 40-bit timestamp and 12 unchanged randomness
  bytes. No byte order is host-dependent.
- **One ordering.** Struct comparison, ordinal text comparison and unsigned bytewise
  binary comparison agree.
- **Fail closed.** Invalid timestamps and invalid destinations are rejected before any
  entropy is consumed or any partial value is returned.
- **No same-second ordering.** Two IDs generated in the same second are ordered by
  randomness, never by generation order. No monotonic counter is promised.

## Deliberate differences from other languages

These are language idioms, not parity gaps:

- .NET returns a `readonly struct` value type instead of an object or string wrapper.
- Span overloads (`ReadOnlySpan<char>`, `ReadOnlySpan<byte>`, `Span<byte>`) avoid
  allocations; the string and array overloads have identical semantics.
- `TimestampUtc` is a `DateTimeOffset`; other SDKs may return a native date-time or
  epoch seconds.
- `PikaidFormatException` derives from `FormatException` so existing .NET error handling
  keeps working.

Any change that removes, renames or weakens a row above is a breaking API change and
must be reviewed as such.
