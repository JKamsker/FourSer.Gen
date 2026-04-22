# NullCollectionDynamicCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/NullCollectionDynamicCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/NullCollectionDynamicCount/NullCollectionDynamicCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `null` and empty collections are encoded identically. Serialization writes a zero count for `null`, but deserialization always materializes an empty `List<Item>`, so `null` cannot round-trip.
- `Low`: `GetPacketSize` accepts `null` collection elements because `Item.GetPacketSize(null)` returns `0`, while serialization rejects them.

## Notes
- No additional confirmed batching gap was found for this reference-type collection.
