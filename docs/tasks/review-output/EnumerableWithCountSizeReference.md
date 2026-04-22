# EnumerableWithCountSizeReference
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/EnumerableWithCountSizeReference/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/EnumerableWithCountSizeReference/EnumerableWithCountSizeReference.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `CountSizeReference` is treated as derived data, not serialized state. The serializer recomputes the count from `MyList`, backfills it, and can round-trip a different `Count` than the source object held.
- `Low`: `GetPacketSize` materializes the enumerable into an array just to count and size it, adding avoidable allocation.

## Notes
- No separate batching issue was confirmed because each `Entity` contains a variable-length string payload.
