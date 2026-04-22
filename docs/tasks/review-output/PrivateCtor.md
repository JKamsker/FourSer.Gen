# PrivateCtor
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PrivateCtor/PrivateCtor.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The generator synthesizes a private value constructor even though it is already inside the partial type and could call the existing private parameterless constructor, then assign `Value`. That is redundant and could bypass future initialization logic in the original ctor.

## Notes
- The generated serializer/deserializer shape otherwise matches the test intent.
- There is no meaningful batching opportunity for this single 4-byte scalar payload.
