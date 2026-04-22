# ConcreteInheritanceCollection
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/ConcreteInheritanceCollection/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/ConcreteInheritanceCollection/ConcreteInheritanceCollection.RunGeneratorTest.verified.txt`

## Findings
- `Low`: `GetPacketSize` accepts `null` `Dogs` elements because `Dog.GetPacketSize(null)` returns `0`, but serialization rejects `null` items.

## Notes
- Inheritance flattening otherwise matches the expected `Dog : Pet` shape.
- No confirmed batching win was found for `List<Dog>`.
