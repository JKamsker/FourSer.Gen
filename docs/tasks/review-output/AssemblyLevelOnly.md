# AssemblyLevelOnly Review

## Verdict
Functionally correct and aligned with likely user expectations. The assembly-level `[DefaultSerializer]` declared in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelOnly/input.cs:5` is consistently honored for `Packet.Name` in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelOnly/AssemblyLevelOnly.RunGeneratorTest.verified.txt:28`, `:44`, `:57`, `:68`, and `:83`. No material correctness findings.

## Findings
- Low: `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelOnly/AssemblyLevelOnly.RunGeneratorTest.verified.txt:2-15` emits the standard template header even though this case never uses most of it. Because all packet work is delegated directly to `AssemblyStringSerializer`, the extra imports and aliases (`System.Buffers`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, `System.Text`, `FourSer.Gen.Helpers`, `SpanReader`, `StreamReader`, `SpanWriter`, `StreamWriter`) are redundant generated code rather than required dependencies.

## Performance opportunities
- No meaningful batching or stream/IO reduction opportunity is visible at the generated `Packet` wrapper level. The generated methods already collapse to a single cached serializer call for the only field in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelOnly/AssemblyLevelOnly.RunGeneratorTest.verified.txt:28`, `:44`, `:57`, `:68-69`, and `:83`.
- The only obvious improvement in this snapshot is trimming the unused header/import template in `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelOnly/AssemblyLevelOnly.RunGeneratorTest.verified.txt:2-15`, which would reduce generated source size and incremental compilation work slightly.

## Open questions / assumptions
- Assumed this fixture is intentionally a coverage/naming variant of `tests/FourSer.Tests/GeneratorTestCases/AssemblyLevelDefaultSerializer/input.cs:5`; the generated behavior appears equivalent apart from type names.
- I could not complete `dotnet test --filter "DisplayName~AssemblyLevelOnly"` locally because the test host requires `Microsoft.NETCore.App 9.0.0`, which is not installed on this machine. The verdict above is therefore based on static inspection plus the successful build portion of the test command, not a completed xUnit run.
