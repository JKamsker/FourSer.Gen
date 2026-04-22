# GlobalNamespace Review

## Verdict
No material functional findings. `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/input.cs:3-6` defines a global-namespace partial class with one `int` property, and `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/GlobalNamespace.RunGeneratorTest.verified.txt:17-81` correctly emits a matching serializer in the global namespace with the expected size calculation, span/stream deserialize paths, and span/stream serialize paths. This also matches the generator's intended global-namespace handling in `src/FourSer.Gen/TypeInfoProvider.cs:56-58` and `src/FourSer.Gen/SerializerGenerator.cs:312-316`, so it is likely what a user would expect from this test case.

## Findings
1. Low: The generated file includes redundant header usings for this case. `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/GlobalNamespace.RunGeneratorTest.verified.txt:2-11` emits `System`, `System.Buffers`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, `System.Text`, `System.IO`, and `FourSer.Gen.Helpers`, but this file only needs `FourSer.Contracts` plus the helper aliases at `:9` and `:12-15`. This comes from the unconditional header emitted in `src/FourSer.Gen/SerializerGenerator.cs:296-310`. It is not functionally wrong, but it is duplicate/redundant generated code and adds avoidable generated-source noise.

## Performance opportunities
- No additional batching or stream/IO reduction looks warranted for this specific shape. The payload is a single 4-byte `int` in `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/input.cs:6`, while the generator's batching threshold is 8 bytes in `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs:13-17`. The scalar reads/writes in `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/GlobalNamespace.RunGeneratorTest.verified.txt:42`, `:55`, `:66`, and `:80` are therefore consistent with current optimization policy.
- Trimming the unused header usings in `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/GlobalNamespace.RunGeneratorTest.verified.txt:2-11` would slightly reduce generated source size and Roslyn parse/bind work. Runtime impact should be negligible.

## Open questions / assumptions
- Assuming the purpose of this fixture is to verify that a `[GenerateSerializer]` type in the global namespace generates without an invalid `namespace` declaration, the current output matches that expectation.
- I could not execute `GeneratorTests.RunGeneratorTest(GlobalNamespace)` locally because `tests/FourSer.Tests` targets .NET 9 and this machine does not have the .NET 9 runtime installed. The review is therefore based on static inspection of the checked-in snapshot and generator code paths.
