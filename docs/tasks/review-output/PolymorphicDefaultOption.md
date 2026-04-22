# PolymorphicDefaultOption
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicDefaultOption/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicDefaultOption/PolymorphicDefaultOption.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: Homogeneity is not validated for `SingleTypeId` collections. Mixed concrete types can size successfully and then fail mid-write with `InvalidCastException`.
- `Low`: `IReadOnlyCollection<IItem>` deserialization pays for an unnecessary second `List<IItem>`.
- `Low`: `ReadOnlyItems` resolves the same discriminator twice during serialization.

## Notes
- The default-discriminator behavior for empty collections appears intentional and is backed by behavioural tests in the repo.
