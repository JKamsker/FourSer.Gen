# NullProperty
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/NullProperty/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/NullProperty/NullProperty.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` accepts `MainObject.Nested == null` via `NestedObject.GetPacketSize(null) == 0`, but serialization throws for the same state.

## Notes
- No other confirmed duplicate logic or batching opportunity was found for this small nested payload.
