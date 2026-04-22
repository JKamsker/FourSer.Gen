# NestedObject
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/NestedObject/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/NestedObject/NestedObject.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` treats a required nested member as zero bytes when `null`, but serialization only discovers the problem after writing earlier data and then throws.
- `Low`: Stream fusion could plausibly extend one field further and avoid one extra write inside `NestedData.Serialize(Stream)`.

## Notes
- The generated shape otherwise matches the source intent.
