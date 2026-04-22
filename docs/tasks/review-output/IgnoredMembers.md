# IgnoredMembers
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/IgnoredMembers.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The generated type emits an unnecessary constructor pair for an otherwise fully mutable shape.
- `Low`: Fixed packet size is not collapsed to a constant return even though only one byte is serialized after ignored members are filtered out.

## Notes
- Ignore handling itself is correct: only `Included` is serialized.
- No batching opportunity exists for this 1-byte payload.
