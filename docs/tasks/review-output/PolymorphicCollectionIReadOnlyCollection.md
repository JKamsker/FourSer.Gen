# PolymorphicCollectionIReadOnlyCollection
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyCollection/PolymorphicCollectionIReadOnlyCollection.RunGeneratorTest.verified.txt`

## Findings
- `High`: Later elements are not validated against the chosen single discriminator. A later `null` or wrong-type element can change payload semantics or fail late with `InvalidCastException`.
- `Medium`: The stream serializer backpatches the count as if this were a seek-only enumerable even though `IReadOnlyCollection<T>` already exposes `Count`.
- `Low`: Deserialization allocates and copies a second `List<IItem>` even though the staging list already satisfies `IReadOnlyCollection<IItem>`.

## Notes
- A further size-path optimization was noted as speculative only.
