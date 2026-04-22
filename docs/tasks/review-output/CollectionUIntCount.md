# CollectionUIntCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionUIntCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionUIntCount/CollectionUIntCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` accepts `null` collection items that serialization rejects, so pre-sizing can succeed for an object graph that fails during write.
- `Low`: The generated collection path repeats count/property evaluation and re-casts the count instead of caching those values locally.

## Notes
- The `uint` count prefix and inherited member handling otherwise look correct.
- No meaningful batching improvement was confirmed for the nested `Cat` payloads.
