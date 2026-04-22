# PrivateCtor Review

## Verdict
Functionally correct for `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/input.cs`: the generated serializer writes and reads the single `int` member consistently, and the generated partial does not require the user to expose a public constructor. It is only partially aligned with likely user expectations for a fixture named `PrivateCtor`, because deserialization never uses the user-authored private constructor.

## Findings
### Medium
The generated code bypasses the user-declared private constructor instead of constructing through it. `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/input.cs:8` declares the only user constructor as `private PrivateCtorPacket() { }`, but `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:32` emits a new `private PrivateCtorPacket(int value)` and both deserialize paths instantiate that synthetic constructor at `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:41` and `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:54`. For this exact fixture the original constructor is empty, so behavior is still correct, but the case does not verify that private-constructor logic is preserved. The generator currently chooses this path in `src/FourSer.Gen/TypeInfoProvider.cs:639`, `src/FourSer.Gen/TypeInfoProvider.cs:668`, `src/FourSer.Gen/SerializerGenerator.cs:220`, `src/FourSer.Gen/SerializerGenerator.cs:331`, and `src/FourSer.Gen/CodeGenerators/Emission/PlanTypeEmitter.cs:89`.

### Low
The generated file header contains unused imports, which is redundant codegen noise for such a small type. In `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:2-15`, only `FourSer.Contracts` and the alias imports are needed by the emitted body; the rest of the header is unused in this snapshot. That boilerplate is emitted unconditionally from `src/FourSer.Gen/SerializerGenerator.cs:294-310`.

## Performance opportunities
- No material batching or stream/IO reduction opportunity in this specific snapshot. `Deserialize(Stream)` performs a single `ReadInt32` at `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:53`, and `Serialize(Stream)` performs a single `WriteInt32` at `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt:78`.
- The only realistic improvement here is code-size related rather than runtime-related: if all members are writable and a parameterless constructor already exists, the generator could avoid emitting the extra value constructor entirely.

## Open questions / assumptions
- Assumed intent of the `PrivateCtor` fixture is to verify that a type with a private constructor is handled in a way that respects that constructor, not merely that serialization works without a public constructor.
- I could not execute the xUnit case end-to-end on this machine: `dotnet test tests/FourSer.Tests/FourSer.Tests.csproj --filter "DisplayName~PrivateCtor"` built successfully but failed to start `testhost.exe` because the .NET 9 runtime is not installed. The verdict is therefore based on source inspection plus successful build, not an executed test run.
