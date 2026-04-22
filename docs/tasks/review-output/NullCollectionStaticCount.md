# NullCollectionStaticCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/NullCollectionStaticCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/NullCollectionStaticCount/NullCollectionStaticCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `null` collections are canonicalized to empty lists and cannot round-trip back to `null`.
- `Low`: `GetPacketSize` is looser than serialization for `null` elements inside the collection.
- `Info`: Deserialization emits a redundant temporary list local before constructor assignment.

## Notes
- No concrete batching/coalescing win was confirmed for this exact pair.
