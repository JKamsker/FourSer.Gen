# PropertyLevelSerializer
Status: `no-findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PropertyLevelSerializer/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PropertyLevelSerializer/PropertyLevelSerializer.RunGeneratorTest.verified.txt`

## Notes
- The generated code matches intent: `Name` uses the property-level serializer while `Description` stays on the built-in string path.
- No confirmed duplicate logic or safe missed batching opportunity was found. Custom serializers are treated as opaque and excluded from batching.
- Residual note: the fixture custom serializer stubs its stream methods, so this case proves routing rather than real stream round-trip behavior.
