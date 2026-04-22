# PolymorphicIndividualTypeIds
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicIndividualTypeIds/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicIndividualTypeIds/PolymorphicIndividualTypeIds.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` mishandles `null` items and fails with an accidental `NullReferenceException` instead of the serializer's explicit item validation.
- `Low`: The stream path could potentially fuse the per-item discriminator with the fixed derived-type prefix into one write.

## Notes
- The case otherwise matches intent: per-item type ids, correct Cat/Dog dispatch, and flattened inherited `Animal.Id`.
- One subagent could run the targeted snapshot and compile tests successfully using `DOTNET_ROLL_FORWARD=Major`.
