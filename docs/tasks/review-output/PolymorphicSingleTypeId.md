# PolymorphicSingleTypeId
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicSingleTypeId/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicSingleTypeId/PolymorphicSingleTypeId.RunGeneratorTest.verified.txt`

## Findings
- `High`: `TypeIdProperty` is not treated as caller-owned state. Empty collections hardcode the first option and non-empty collections infer the value from the first element, so independently assigned `AnimalTypeId` values do not round-trip.
- `Medium`: Homogeneity is only inferred from the first element. Mixed collections can pass `GetPacketSize` and then fail mid-serialize with `InvalidCastException`.
- `Low`: The discriminator is resolved twice and the 3-byte header is still emitted as separate operations instead of one tiny-header fast path.

## Notes
- No larger batching issue was found inside the generated `Cat` and `Dog` serializers.
