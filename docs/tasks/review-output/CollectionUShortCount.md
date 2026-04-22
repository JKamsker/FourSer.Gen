# CollectionUShortCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionUShortCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionUShortCount/CollectionUShortCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` accepts `null` collection items that serialization rejects.
- `Low`: Deserialization emits redundant `checked((int)...)` conversions even though `ushort -> int` is already safe.

## Notes
- The `ushort` count handling itself looks correct.
- No safe collection-level bulk path was confirmed for `List<Cat>`.
