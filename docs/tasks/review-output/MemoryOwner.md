# MemoryOwner
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/MemoryOwner/MemoryOwner.RunGeneratorTest.verified.txt`

## Findings
- `High`: Nullable `IMemoryOwner<T>` values do not round-trip. `null` is serialized as a zero count but deserialization always rents an empty owner, changing observable state and ownership semantics.
- `High`: Deserialization can leak rented pooled buffers on failure because owners are acquired before fallible reads and are not protected by cleanup on exception.
- `Medium`: Nullable nested `Child?` is treated as required during serialization, while `GetPacketSize` still accepts `null`.

## Notes
- Bulk byte IO for the memory-owner payloads is already present, so no extra batching gap stood out here.
