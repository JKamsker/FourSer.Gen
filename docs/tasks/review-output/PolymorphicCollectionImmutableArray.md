# PolymorphicCollectionImmutableArray
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableArray/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableArray/PolymorphicCollectionImmutableArray.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: `GetPacketSize` mishandles `null` items in the polymorphic immutable array and fails differently from the serializer's explicit collection-item validation.
- `Low`: Deserialization stages through `List<IItem>` and `ImmutableArray.CreateRange(...)`, paying an avoidable extra allocation and copy.

## Notes
- The overall wire shape otherwise matches the intended count-prefixed, order-preserving polymorphic array.
