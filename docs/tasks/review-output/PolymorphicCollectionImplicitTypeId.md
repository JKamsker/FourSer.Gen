# PolymorphicCollectionImplicitTypeId
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImplicitTypeId/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImplicitTypeId/PolymorphicCollectionImplicitTypeId.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` and `Serialize` disagree on `SingleTypeId` semantics. Sizing still switches per element, while serialization commits to the first discriminator and then hard-casts the rest.
- `Low`: The adjacent count and discriminator fields on the stream path are a plausible batching opportunity.

## Notes
- Aside from those points, the output matches the intended implicit `SingleTypeId` collection shape.
