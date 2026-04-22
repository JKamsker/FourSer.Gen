# ClassLevelDefaultSerializer
Status: `no-findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/ClassLevelDefaultSerializer.RunGeneratorTest.verified.txt`

## Notes
- The class-level default serializer for `string` is applied consistently to both `Name` and `Description`.
- The generated output reuses one cached serializer instance and does not emit duplicate serializer fields.
- No confirmed batching or coalescing opportunity was found for this case without changing the `ISerializer<T>` contract.
