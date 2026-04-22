# RecordTypes
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/RecordTypes/RecordTypes.verified.cs`

## Findings
- `High`: The snapshot adds duplicate constructors to positional records, which is illegal and does not match current generator behavior.
- `High`: The snapshot's serialize signatures do not satisfy the current `ISerializable<T>` contract and are stale relative to the live generator.
- `Medium`: `GetPacketSize` uses null-unsafe string sizing while the string writers serialize `null` like empty, so sizing and serialization disagree.
- `Low`: This case is not wired into the active snapshot harness because the folder contains `RecordTypes.cs` instead of `input.cs`.

## Notes
- This looks like a stale, disconnected snapshot rather than a current generator output regression in the active test path.
