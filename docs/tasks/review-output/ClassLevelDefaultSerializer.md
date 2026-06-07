# ClassLevelDefaultSerializer Review

## Verdict
Partially correct. The snapshot correctly applies the class-level default serializer to both `string` members and reuses one cached serializer instance, which matches the likely intent of this test case. It is not fully functional as generated, though, because the public stream overloads delegate to placeholder stream methods in the custom serializer.

## Findings
- High: `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/ClassLevelDefaultSerializer.RunGeneratorTest.verified.txt:62-70` and `:92-100` call `MyCustomStringSerializer.Deserialize(Stream)` and `Serialize(string, Stream)` for both string members. In `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/input.cs:17` and `:25`, those methods are stubs (`Serialize` does nothing and `Deserialize` always returns `""`). Any `Stream` round-trip therefore drops `Name` and `Description`.
- Low: `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/ClassLevelDefaultSerializer.RunGeneratorTest.verified.txt:2-15` emits the full generic header even though this case only needs a small subset of it. That is harmless, but it is redundant snapshot noise. There is no material duplicate serialization logic beyond that header; the shared serializer cache at `:114-116` is the right deduplication.

## Performance opportunities
- `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs:122-124` and `:225-230` disable batching whenever a resolved serializer exists, so this case cannot coalesce IO for the two string fields.
- In the generated stream path, `Id`, `Name`, and `Description` are serialized and deserialized as separate operations at `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/ClassLevelDefaultSerializer.RunGeneratorTest.verified.txt:67-69` and `:98-100`. A fallback that uses `GetPacketSize` plus span serialization into a stack or pooled buffer could reduce stream calls and make custom/default serializers cheaper on the stream path.

## Open questions / assumptions
- I assumed consumers expect both span and stream overloads to be usable, not just the snapshot text to compile.
- I assumed null `string` values are out of scope for this fixture; the custom serializer in `tests/FourSer.Tests/GeneratorTestCases/ClassLevelDefaultSerializer/input.cs` does not handle them.
- If the test only intends to validate class-level default serializer selection, the selection itself looks correct and aligns with likely user expectations.
