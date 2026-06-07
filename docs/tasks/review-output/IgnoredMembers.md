# IgnoredMembers Review

## Verdict
Functionally correct and consistent with likely user expectations. The input declares one included field and two explicitly ignored fields in `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/input.cs:5-14`, and the generated serializer only sizes, reads, and writes `Included` in `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/IgnoredMembers.RunGeneratorTest.verified.txt:21-29`, `:41-45`, `:54-58`, `:62-68`, and `:76-82`. That matches the generator's serializable-member filter for both `[Ignored]` and `[IgnoreDataMember]` in `src/FourSer.Gen/TypeInfoProvider.cs:800-812`.

The targeted `RunGeneratorTest(testCaseName: "IgnoredMembers")` and `GeneratedSource_ShouldCompile(testCaseName: "IgnoredMembers")` cases passed locally.

## Findings
No material findings.

1. Low: The generated file carries the full shared import template even though most of it is unused for this one-byte packet. `System`, `System.Buffers`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, `System.Text`, `System.IO`, and `FourSer.Gen.Helpers` are redundant in `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/IgnoredMembers.RunGeneratorTest.verified.txt:2-15`. This comes from the unconditional header emitted by `src/FourSer.Gen/SerializerGenerator.cs:294-310`. It increases generated source size slightly but does not affect behavior.

## Performance opportunities
- No case-specific batching or stream/IO reduction opportunity is visible here. After filtering the two ignored members, the payload is a single byte, so each path already performs one byte-sized operation in `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/IgnoredMembers.RunGeneratorTest.verified.txt:28`, `:44`, `:57`, `:68`, and `:82`. That is also below the generator's batching floor of 8 bytes in `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs:13-16`.
- Minor codegen cleanup only: `tests/FourSer.Tests/GeneratorTestCases/IgnoredMembers/IgnoredMembers.RunGeneratorTest.verified.txt:27-29`, `:43-46`, and `:56-59` keep constant-size and temporary scaffolding (`var size = 0; size += 1;`, `included`, `obj`) that could be collapsed. This would reduce emitted text a bit, but it is not a runtime-significant issue.

## Open questions / assumptions
- Assumption: ignored members are intended to be excluded from the wire format and to rehydrate to default values on deserialize. This case matches that behavior.
- Assumption: this test case is specifically about attribute-based member exclusion; it does not exercise field initializers or user-defined constructor interactions on ignored members.
