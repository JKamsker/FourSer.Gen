# PolymorphicCollectionIReadOnlyList
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyList/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionIReadOnlyList/PolymorphicCollectionIReadOnlyList.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: Null elements are mishandled in the `SingleTypeId` path. A first `null` item fails via `firstItem.GetType()`, while a later `null` item becomes a no-op serialize after the count is already written.
- `Low`: Deserialization clones the staging list before assigning to `IReadOnlyList<IItem>`, which is redundant because `List<IItem>` already satisfies the target interface.

## Notes
- Mixed concrete types were not counted as a finding here because `SingleTypeId` is the documented homogeneous mode.
