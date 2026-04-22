# RecordTypes Review

## Verdict
Functionally correct and aligned with likely user expectations for `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/input.cs:5-9`. The generated code preserves the primary-constructor parameter order for both records, emits the expected null guard only for the reference-type record class, and reconstructs both records through their declared constructors in both span and stream paths (`tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.RunGeneratorTest.verified.txt:19-111`, `:133-214`). No material functional findings.

## Findings
No material correctness or expectation findings. Low-severity redundancy notes:

- Low: The snapshot repeats the full generated file preamble for each record serializer, and several of those imports are unused in this fixture. `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.RunGeneratorTest.verified.txt:1-15` and `:115-129` duplicate the same header and aliases even though the two serializers only need a subset of them. This is harmless, but it is redundant generated code.
- Low: The fused stream UTF-8 write path is emitted twice almost verbatim, once for `RecordClass.B` and once for `RecordStruct.D`. `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.RunGeneratorTest.verified.txt:80-109` and `:182-211` duplicate the same inline algorithm instead of reusing a specialized helper. That is still functionally correct, but it increases generated code size and maintenance surface.

## Performance opportunities
- No meaningful fixed-width batching is available under the current batching rules because each record only contributes one 4-byte scalar before the string, below the default 8-byte minimum (`src/FourSer.Gen/FourSerGeneratorOptions.cs:18-24`, `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs:12-21`).
- The stream serializers still perform two writes per record: one `WriteInt32` for `A` or `C`, then one fused write for the string block (`tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.RunGeneratorTest.verified.txt:79-93` and `:181-195`). If the generator ever supports mixed fixed-width plus variable-width fusion, it could combine the leading `int` and the string length/payload into one temporary buffer and one `stream.Write(...)` per record.
- If generated code size matters, the duplicated inline string-fusion block could move behind a specialized helper. That is primarily a maintainability and JIT-footprint improvement rather than a large runtime gain, but this test case shows the duplication clearly.

## Open questions / assumptions
- I assumed the project intentionally canonicalizes `null` and `string.Empty` to the same wire representation for strings. That is the established runtime behavior in `src/FourSer.Gen/Resources/Code/StringHelper.cs:11-18`, `:22-60`, `src/FourSer.Gen/Resources/Code/StreamReaderHelpers.cs:103-130`, and `src/FourSer.Gen/Resources/Code/StreamWriterHelpers.cs:120-155`, so `RecordClass.B` and `RecordStruct.D` follow repo convention even though a runtime `null` would deserialize back as `string.Empty`.
- I assumed the verified snapshot is a concatenation of separate generated `*.g.cs` files, not one compilation unit, consistent with `tests/FourSer.Tests/GeneratorTests.cs:1019-1028`. Under that convention, the second `using` block at `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.RunGeneratorTest.verified.txt:115-129` is redundant snapshot noise rather than a compile problem.
