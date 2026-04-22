# ClassLevelOverAssembly Review

## Verdict
Functionally correct and consistent with likely user expectations. The input declares an assembly-level `string` serializer and then overrides it at the class level for `Packet` in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/input.cs:5,10-13,25-31`. The generated code consistently uses `ClassStringSerializer` for `GetPacketSize`, span deserialization, stream deserialization, span serialization, and stream serialization in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/ClassLevelOverAssembly.RunGeneratorTest.verified.txt:21-29,41-45,54-58,62-69,77-83`. That precedence matches both the generator merge logic in `src/FourSer.Gen/TypeInfoProvider.cs:583-621` and the documented behavior in `README.md:475-499`. There is no redundant serializer-cache entry for the assembly-level serializer; only the winning `ClassStringSerializer` is cached in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/ClassLevelOverAssembly.RunGeneratorTest.verified.txt:97-99`.

The targeted `RunGeneratorTest(testCaseName: "ClassLevelOverAssembly")` and `GeneratedSource_ShouldCompile(testCaseName: "ClassLevelOverAssembly")` cases passed locally.

## Findings
No material findings.

- Low: The generated file still emits the full shared import/alias template even though most of it is unused in this case. `System.Buffers`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, `System.Text`, `System.IO`, `FourSer.Gen.Helpers`, and the helper aliases are redundant in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/ClassLevelOverAssembly.RunGeneratorTest.verified.txt:3-15`. This increases generated source size slightly but does not affect behavior.

## Performance opportunities
- No case-specific batching or stream/IO reduction opportunity is visible here. This packet has a single `string` member, and each code path already delegates to exactly one custom serializer call in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/ClassLevelOverAssembly.RunGeneratorTest.verified.txt:28,44,57,68,83`.
- Minor compile-time/code-size cleanup only: pruning unused imports and aliases from `tests/FourSer.Tests/GeneratorTestCases/ClassLevelOverAssembly/ClassLevelOverAssembly.RunGeneratorTest.verified.txt:2-15` would reduce parsing and emitted text slightly, but it would not materially change runtime performance.

## Open questions / assumptions
- Assumption: class-level `DefaultSerializer` entries are intended to override assembly-level defaults for the same target type. That assumption is consistent with `src/FourSer.Gen/TypeInfoProvider.cs:604-616` and `README.md:475-499`.
