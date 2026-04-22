# GlobalNamespace
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/GlobalNamespace/GlobalNamespace.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: The generated serializer silently treats `null` as a zero-length/no-op case for a required reference type, which cannot round-trip back through `Deserialize(...)`.
- `Low`: Fixed packet-size generation misses a trivial constant-fold and still emits `var size = 0; size += 4; return size;`.

## Notes
- Global-namespace handling itself looks correct.
- No real batching opportunity exists for the single 4-byte scalar payload.
