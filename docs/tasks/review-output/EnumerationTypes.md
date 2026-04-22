# EnumerationTypes
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/EnumerationTypes/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/EnumerationTypes/EnumerationTypes.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: The fixed-size `LargeBag` collection constraint is not enforced in `GetPacketSize`, so size calculation can succeed for a collection shape that serialization rejects.
- `Low`: `IList<double>` misses an available bulk unmanaged write path and is serialized element-by-element.
- `Low`: `GetPacketSize` needlessly materializes `Data` with `ToArray(...)` even when the runtime value may already expose a count.

## Notes
- The rest of the generated shape appears aligned with the source intent.
