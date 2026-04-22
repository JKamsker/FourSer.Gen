# CollectionByteCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionByteCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionByteCount/CollectionByteCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` accepts `null` collection items because `Cat.GetPacketSize(null)` returns `0`, but serialization rejects `null` items after the count is already written. Size calculation and serialization enforce different validity rules.

## Notes
- The `byte` count-prefix behavior itself looks correct.
- No clear collection-level batching win was confirmed beyond the existing fused handling inside `Cat.Name`.
