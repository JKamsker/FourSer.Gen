# ListOfStructs Review

## Verdict
Functionally correct for the modeled `List<MyStruct>` round-trip in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/input.cs`. I did not find a material correctness bug in the emitted serializer. The main gap is that the generated code does not match likely user expectations for the generator's default optimization mode, because this trivially fixed-size struct list still falls back to per-element work instead of a bulk path.

## Findings
### Medium - Default-mode collection fast path is missed for a trivially fixed-size struct list
- `MyStruct` is only a single `int` field in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/input.cs:3-6`, and the generated serializer confirms each element is always 4 bytes and encoded as one `Int32` in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:21-68`.
- Even so, the list serializer still sizes the collection item-by-item in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:99-105`, deserializes item-by-item in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:121-146`, and serializes item-by-item in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:164-193`.
- That is surprising under the default `AggressivePortable` mode, because collection fast paths are enabled by default in `src/FourSer.Gen/FourSerGeneratorOptions.cs:18-24` and `src/FourSer.Gen/FourSerGeneratorOptions.cs:44-45`. The current optimizer explicitly excludes collection elements that also have `[GenerateSerializer]` in `src/FourSer.Gen/CodeGenerators/Optimization/CollectionFastPathPass.cs:132-137`, so `List<MyStruct>` misses batching even though this specific generated serializer is layout-trivial.

### Low - Redundant generated boilerplate is repeated for both emitted types
- The full prologue/import block is emitted once for `MyStruct` and then repeated for `PacketWithListOfStructs` in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:1-15` and `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:72-86`.
- This is harmless at runtime, but it is redundant source bloat and makes the verified output harder to review than necessary.

## Performance opportunities
- Batch `Structs` span/stream reads and writes as contiguous bytes when a generated struct serializer is provably fixed-size and little-endian compatible. In this case that would replace the per-item calls in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:121-146` and `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:164-193` with one count prefix plus one bulk copy/read over the list span.
- Fold packet sizing from the per-item loop in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:99-105` to `sizeof(int) + obj.Structs.Count * 4`, since `MyStruct.GetPacketSize(...)` is constant `4` in `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:21-25`.
- Cache `checked((int)structsCount)` once during deserialization instead of repeating it in both the list-capacity expression and loop condition at `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:123-124` and `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:142-143`.

## Open questions / assumptions
- I assumed collapsing `null` and empty collections is intentional here. `Serialize` writes count `0` for `null` at `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:158-160` and `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:183-185`, while deserialize always materializes a new list at `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:123-129` and `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt:142-148`. That matches other collection fixtures in this repo, so I did not score it as a defect for this case.
- I could not run the filtered `FourSer.Tests` cases locally because the machine is missing `Microsoft.NETCore.App` `9.0.0`; the review is otherwise based on source inspection and generator behavior.
