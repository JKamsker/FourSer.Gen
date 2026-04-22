# SimpleConcreteCollection
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/SimpleConcreteCollection/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/SimpleConcreteCollection/SimpleConcreteCollection.RunGeneratorTest.verified.txt`

## Findings
- `Low`: `GetPacketSize` accepts `null` `Cats` elements because `Cat.GetPacketSize(null)` returns `0`, while serialization rejects those same elements.

## Notes
- The rest of the shape aligns with the source intent, including inherited `Animal.Id`.
- Speculative only: per-`Cat` stream writes could potentially fuse one more fixed-size field with the string block.
