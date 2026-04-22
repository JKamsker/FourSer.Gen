# PolymorphicCollectionImmutableQueue
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableQueue/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableQueue/PolymorphicCollectionImmutableQueue.RunGeneratorTest.verified.txt`

## Findings
- `High`: An empty queue discards the caller-supplied `TypeIdProperty`. The serializer falls back to the default option (`10`) for empty collections, so a stale explicit property value is overwritten on round-trip.
- `Medium`: The serializer performs redundant full traversals of `ImmutableQueue<T>` and misses an obvious batched 5-byte header write (`byte typeId + int count`).

## Notes
- Review was static only for this case.
