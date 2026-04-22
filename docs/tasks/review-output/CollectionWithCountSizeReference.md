# CollectionWithCountSizeReference
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/CollectionWithCountSizeReference/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/CollectionWithCountSizeReference/CollectionWithCountSizeReference.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The serializer resolves the collection discriminator twice per overload, once for the header and again for the payload switch.
- `Low`: The fixed 8-byte `TypeId + Count` header is serialized as two separate writes even though deserialization already reads it as one combined block.

## Notes
- No confirmed functional mismatch was reported for this case beyond the redundant work and missed batching.
