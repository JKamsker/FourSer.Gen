# AssemblyLevelDefaultSerializer Review

## Verdict
Functionally correct and aligned with likely user expectations. The assembly-level `[DefaultSerializer]` declared in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/input.cs:5` is consistently honored for `Packet.Name`, and the generated `GetPacketSize`, span deserialization, stream deserialization, span serialization, and stream serialization paths all route through the cached `MyCustomStringSerializer` in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/AssemblyLevelDefaultSerializer.RunGeneratorTest.verified.txt:28`, `:44`, `:57`, `:68-69`, and `:83`. That matches the generator's assembly-level default-serializer lookup in `src/FourSer.Gen/TypeInfoProvider.cs:583-602` and the documented assembly-level behavior in `README.md:495-499`. No material correctness or expectation-mismatch findings.

## Findings
No material findings.

- Low: `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/AssemblyLevelDefaultSerializer.RunGeneratorTest.verified.txt:2-15` emits the full shared import/alias header even though this case never uses most of it. Because all member work is delegated directly to `MyCustomStringSerializer`, the extra imports and aliases (`System.Buffers`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, `System.Text`, `FourSer.Gen.Helpers`, `SpanReader`, `StreamReader`, `SpanWriter`, `StreamWriter`) are redundant generated code rather than required dependencies. This comes from the unconditional header template in `src/FourSer.Gen/SerializerGenerator.cs:294-310`.

## Performance opportunities
- No meaningful batching or stream/IO reduction opportunity is visible in the emitted `Packet` wrappers. There is only one field, and both the span and stream paths already collapse to a single cached serializer call in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/AssemblyLevelDefaultSerializer.RunGeneratorTest.verified.txt:62-84`.
- Custom/default serializers are treated as an opaque call boundary in `src/FourSer.Gen/CodeGenerators/Planning/SerializePlanBuilder.cs:139-153`, so the built-in string-fusion and scalar-batching optimizations do not apply here. For this one-member packet that is not a material loss; any further stream/IO reduction would need to happen inside `MyCustomStringSerializer`.
- Minor compile-time/code-size cleanup only: trimming the unused header in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/AssemblyLevelDefaultSerializer.RunGeneratorTest.verified.txt:2-15` would reduce generated source size and Roslyn parsing work slightly, but it would not materially change runtime behavior.

## Open questions / assumptions
- Assumed this fixture is intended to verify assembly-level serializer selection, not end-to-end round-trip semantics, because the serializer implementation in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/input.cs:15-21` is a stub that throws for every method.
- I could not complete `dotnet test --filter "DisplayName~AssemblyLevelDefaultSerializer"` locally because the test host requires `Microsoft.NETCore.App 9.0.0`, which is not installed on this machine. The verdict is therefore based on static inspection and the successful build portion of the test command, not a completed xUnit run.
