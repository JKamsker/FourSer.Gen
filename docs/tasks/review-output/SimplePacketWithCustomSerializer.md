# SimplePacketWithCustomSerializer
Status: `no-findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/SimplePacketWithCustomSerializer/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/SimplePacketWithCustomSerializer/SimplePacketWithCustomSerializer.RunGeneratorTest.verified.txt`

## Notes
- The generated output correctly applies the class-level default serializer for `string` to `Name` across size, deserialize, and serialize paths.
- No confirmed duplicate logic or batching gap was found. The custom serializer boundary intentionally prevents primitive batching and built-in string fusion from applying here.
- Residual note: the fixture custom serializer stubs its stream members, so this case validates generator routing more than real stream round-trip behavior.
