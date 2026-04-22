# ListOfStructs
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/ListOfStructs/ListOfStructs.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The generated code misses a bulk IO path for a list of fixed-size generated structs and still serializes/deserializes element-by-element.
- `Low`: Collection sizing misses a constant-size fold and iterates `MyStruct.GetPacketSize(...)` instead of using `count * 4`.
- `Info`: Deserialization emits redundant `checked((int)...)` conversions around an already-`int` count.

## Notes
- No functional correctness issue was confirmed for this case.
