# PolymorphicCollectionImplicitMode
Status: `findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImplicitMode/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/PolymorphicCollectionImplicitMode/PolymorphicCollectionImplicitMode.RunGeneratorTest.verified.txt`

## Findings
- `Medium`: Mixed-type collections are sized as heterogeneous but serialized as homogeneous. `GetPacketSize` accepts per-item runtime types while `Serialize` hard-commits to the first item's discriminator.
- `Low`: Discriminator resolution is duplicated in both serialize paths.
- `Low`: The fixed 12-byte header (`long typeId + int count`) is emitted as separate operations instead of one batched read/write block.

## Notes
- The implicit `SingleTypeId` behavior itself appears intentional for this case.
