# SimplePacket
Status: `no-findings`
Source: `tests/FourSer.Tests/GeneratorTestCases/SimplePacket/input.cs`
Verified: `tests/FourSer.Tests/GeneratorTestCases/SimplePacket/SimplePacket.RunGeneratorTest.verified.txt`

## Notes
- The generated `LoginPacket` code matches the source definition and test intent. Field order and byte-count math are consistent across size, span, and stream paths.
- The stream-side string handling is intentional fused codegen, not accidental duplication.
- Residual note: `null` and empty strings intentionally collapse to the same zero-length payload, and the 5-byte scalar prefix remains below the default batching threshold, so any further stream batching would be speculative.
