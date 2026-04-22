# AssemblyLevelDefaultSerializer
Status: `no-findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/AssemblyLevelDefaultSerializer.RunGeneratorTest.verified.txt`

## Notes
- The generated code correctly applies the assembly-level default serializer for `Packet.Name` across size, span deserialize, stream deserialize, span serialize, and stream serialize.
- No confirmed duplicate logic or batching gap was found for this case. Default/custom serializers are treated as opaque operations, so there is no obvious safe coalescing win here.
- Residual note: one subagent could run the filtered test successfully; another observed only the broader serializer-instance lifetime question, which appears contractual rather than a case-specific defect.
