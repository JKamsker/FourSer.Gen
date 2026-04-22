# CollectionULongCount
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionULongCount/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionULongCount/CollectionULongCount.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` silently accepts `null` collection items while serialization rejects them, so size and write contracts diverge.
- `Low`: Deserialization repeatedly emits `checked((int)catsCount)` instead of hoisting the conversion once.

## Notes
- The `ulong` count-prefix behavior itself appears correct.
- No additional batching issue was confirmed beyond the redundant conversions.
