# PolymorphicCollectionIEnumerable
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIEnumerable/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIEnumerable/PolymorphicCollectionIEnumerable.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `SingleTypeId` serialization does not enforce homogeneity before writing. A mixed sequence can size successfully and then fail mid-serialize with `InvalidCastException`.
- `Low`: `GetPacketSize` materializes the whole `IEnumerable` with `ToArray(...)` even though a single pass would be sufficient for this case.

## Notes
- Reserving and backpatching the count is expected for a non-indexable `IEnumerable`, so there is no extra confirmed serializer batching gap beyond the sizing allocation.
