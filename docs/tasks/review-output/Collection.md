# Collection
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/Collection/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/Collection/Collection.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The non-little-endian fallback loops repeatedly call `CollectionsMarshal.AsSpan(...)` instead of hoisting the span once. This is redundant codegen in a hot path.

## Notes
- The main behavior is correct, and the little-endian fast path already batches collection payload IO well.
- Further batching for the big-endian fallback is only speculative.
