# Review Output

This folder contains per-case review notes for the 42 `sourcefile/verified` pairs under `tests/FourSer.Tests/GeneratorTestCases`.

Cases without a `verified` pair were intentionally excluded:
- `InvalidNestedCollection`
- `UnlimitedCollection`

## Highest Priority Findings

- `MemoryOwner`: nullable `IMemoryOwner<T>` values do not round-trip, deserialization can leak rented buffers on failure, and a nullable nested child is treated as required.
- `RecordTypes`: the snapshot is stale and disconnected from the active harness; it contains illegal duplicate record constructors and signatures that do not match the current `ISerializable<T>` contract.
- `PolymorphicCollectionImmutableQueue`: empty queues overwrite the caller-supplied `TypeIdProperty`.
- `PolymorphicCollectionIReadOnlyCollection`: later elements are not validated against the chosen single discriminator, so later `null` or wrong-type items can corrupt semantics or fail late.
- `PolymorphicSingleTypeId`: `TypeIdProperty` is effectively treated as derived-from-collection state rather than caller-owned state.

## Common Patterns

- Many collection cases have a `GetPacketSize` vs `Serialize` contract mismatch for `null` elements. Sizing accepts `null` because nested `GetPacketSize(null)` returns `0`, while serialization throws.
- Several required nested/reference members are accepted by `GetPacketSize` but rejected later by `Serialize`, which can leave partially written payloads.
- `SingleTypeId` polymorphic collection cases frequently size heterogeneously but serialize homogeneously from the first element, so mixed collections fail late.
- Multiple interface/immutable collection shapes allocate or copy more than necessary during deserialization, or backpatch counts even when a count is already available.
- A smaller set of cases only show codegen-quality issues such as dead serializer cache entries, duplicate discriminator resolution, or missed tiny-header batching.

## How To Use

- Open the per-case markdown file named after the test case for the detailed findings.
- The files are intentionally concise and focus on confirmed issues first, then speculative performance opportunities.
