# PropertyLevelOverClassAndAssembly
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PropertyLevelOverClassAndAssembly/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PropertyLevelOverClassAndAssembly/PropertyLevelOverClassAndAssembly.RunGeneratorTest.verified.txt`

## Findings
- `Low`: The generated cache still instantiates the class-level default serializer even though every actual path for `Name` uses the property-level serializer. That is dead generated code and one avoidable static allocation.

## Notes
- Property-level precedence over class- and assembly-level defaults is otherwise correct.
