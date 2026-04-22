# WrapperType
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/WrapperType/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/WrapperType/WrapperType.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` skips the null validation that `Serialize` applies to the wrapped `Name` member. A null wrapper payload fails through the delegated wrapper with a generic `NullReferenceException` instead of the clearer member-level validation used during serialization.

## Notes
- The wrapper reuse itself is correct, and no additional duplicate logic or realistic batching opportunity was confirmed for this delegated single-member case.
