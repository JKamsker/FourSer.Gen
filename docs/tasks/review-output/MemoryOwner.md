# MemoryOwner Review

## Verdict

Not correct as generated. The `IMemoryOwner<T>` fast paths are mostly sound and already use bulk reads and writes, but `Parent.DisposableProperty` is declared nullable and emitted as mandatory, and `Parent.Deserialize` can leak the rented `Data` owner when nested deserialization fails.

## Findings

1. High: `Child? DisposableProperty` cannot round-trip `null`.
   - `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/input.cs:15` declares the property nullable.
   - The generated size and serialize paths throw if it is null in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:164`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:166`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:249`, and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:277`.
   - The deserialize path always reads a `Child` directly, with no null marker or alternate null case, in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:201` and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:228`.
   - Result: the generated wire format cannot represent one of the declared input states, which is both a functional mismatch and unlikely user expectation for `Child?`.

2. Medium: `Parent.Deserialize` leaks the rented `Data` owner when `Child.Deserialize` fails.
   - `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:189` to `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:200` and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:216` to `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:227` dispose `dataOwner` only while filling the owner itself.
   - After ownership is assigned to `data`, the next nested read in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:201` and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:228` can still throw, at which point the already-rented `IMemoryOwner<byte>` is lost and never disposed.
   - This matters on truncated or corrupt payloads and on any nested deserializer exception.

3. Low: The generated output contains redundant boilerplate.
   - The same auto-generated banner and using block are emitted twice for the two types in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:1` to `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:15` and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:137` to `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:151`.
   - Several imports in those blocks are unused in this case, including `System.Buffers.Binary`, `System.Collections.Generic`, `System.Linq`, `System.Runtime.CompilerServices`, and `System.Text`.

## Performance opportunities

- Zero-count deserialization still rents from `MemoryPool<T>` and may allocate a `MemoryOwnerWrapper<T>` through `SliceToSize(0)` in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:46`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:72`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:189`, and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:216`, with the wrapper created by `src/FourSer.Gen/Resources/Code/MemoryPoolExtensions.cs:8` to `src/FourSer.Gen/Resources/Code/MemoryPoolExtensions.cs:20`. A cached empty owner would avoid pool traffic and wrapper allocation for the null and empty case.
- The payload paths already use bulk `ReadBytes`, `ReadExactly`, and `WriteBytes` for `byte` and unmanaged `int` memory owners in `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:50`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:76`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:103`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:126`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:193`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:220`, `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:247`, and `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt:275`. The remaining batching opportunity is small: fuse the count-prefix write with the immediate payload write to cut a few tiny stream calls.

## Open questions / assumptions

- I treated nullable `IMemoryOwner<T>` canonicalization (`null` to a zero-length owner) as intentional, not a bug, because `tests/FourSer.Tests/OptimizationCollectionFailureTests.cs:101` to `tests/FourSer.Tests/OptimizationCollectionFailureTests.cs:145` explicitly asserts that behavior.
- I assumed nullable reference annotations should matter for nested generated types as well. The current planning and emission code always throws on null nested references and never emits a null marker in `src/FourSer.Gen/CodeGenerators/Planning/SerializePlanBuilder.cs:194` to `src/FourSer.Gen/CodeGenerators/Planning/SerializePlanBuilder.cs:203`, `src/FourSer.Gen/CodeGenerators/Planning/SizePlanBuilder.cs:24` to `src/FourSer.Gen/CodeGenerators/Planning/SizePlanBuilder.cs:40`, `src/FourSer.Gen/CodeGenerators/Emission/PlanTypeEmitter.cs:34` to `src/FourSer.Gen/CodeGenerators/Emission/PlanTypeEmitter.cs:51`, and `src/FourSer.Gen/CodeGenerators/Planning/DeserializePlanBuilder.cs:123` to `src/FourSer.Gen/CodeGenerators/Planning/DeserializePlanBuilder.cs:132`.
- I could not execute the targeted `FourSer.Tests` cases locally because the installed runtimes are `Microsoft.NETCore.App` `8.0.26` and `10.0.6`, while the test host for this repo requires `9.0.0`.
