# CollectionWithConstSize
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionWithConstSize/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionWithConstSize/CollectionWithConstSize.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` does not enforce the fixed-size collection invariants. It can report a size for a list that is `null`, the wrong length, or contains `null` items, while serialization rejects those same inputs.

## Notes
- No confirmed collection-level batching gap was found because each `Entity` is variable-sized.
- Speculative only: `Entity.Serialize(Stream)` could potentially fuse one more fixed-size field into the buffered string write.
