# PolymorphicCollectionImmutableStack
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableStack/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImmutableStack/PolymorphicCollectionImmutableStack.RunGeneratorTest.verified.txt`

## Findings
- `Low`: `GetPacketSize` handles `null` items differently from `Serialize` and can fail with an accidental `NullReferenceException` instead of the explicit item validation path.
- `Low`: Serialization double-traverses `ImmutableStack<T>` by calling `Count()` and then iterating again.

## Notes
- Stack-order preservation looks correct.
- No stronger batching win was confirmed beyond the extra traversal.
