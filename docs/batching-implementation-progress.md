# Instruction Batching Implementation Progress

**Date**: 2025-12-01  
**Status**: In Progress - Baseline Tests Created, Bug Found

## Summary

Implementing instruction batching for stream deserialization to reduce syscalls by reading consecutive fixed-size fields in a single `ReadExactly` call.

## Completed Work

### 1. Design Document Created
- `docs/instruction-batching-design.md` - Full design with examples, algorithm, and testing strategy

### 2. BatchingUtilities Implementation
- **File**: `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs`
- Contains:
  - `AnalyzeMembers()` - Analyzes members and produces batched/non-batched instructions
  - `IsBatchable()` - Determines if a member can be batched
  - `GetMemberSize()` - Gets byte size of a member
  - `GetBatchReadExpression()` - Generates code to read from batch buffer
  - `GetBatchWriteExpression()` - Generates code to write to batch buffer
  - Record types: `BatchInstruction`, `BatchGroup`, `SingleMember`, `BatchedMember`

### 3. Test Types Created
- **File**: `tests/FourSer.Tests.Behavioural/Batching/BatchTestTypes.cs`
- 12 test types covering various scenarios:
  - `BatchTestAllPrimitives` - All unmanaged types (42 bytes, ideal batch)
  - `BatchTestMixedWithString` - Primitives with string in middle
  - `BatchTestMixedWithCollection` - Primitives with collection in middle
  - `BatchTestLeadingString` - String first, then primitives
  - `BatchTestTrailingCollection` - Primitives, then collection
  - `BatchTestSinglePrimitive` - Single field (should NOT batch)
  - `BatchTestExactThreshold` - Exactly 8 bytes (threshold)
  - `BatchTestBelowThreshold` - 7 bytes (below threshold)
  - `BatchTestMultipleStrings` - Multiple strings interleaved
  - `BatchTestManyBytes` - 20 consecutive bytes
  - `BatchTestWithBooleans` - Booleans mixed with primitives
  - `BatchTestNestedType` - Nested serializable type

### 4. Test Suite Created
- **File**: `tests/FourSer.Tests.Behavioural/Batching/BatchingTests.cs`
- 19 tests covering:
  - Round-trip tests (Span and Stream)
  - Stream vs Span consistency tests
  - Edge cases (empty strings, empty collections, zero values)

### 5. Bug Fix: Missing ReadSByte
- **File**: `src/FourSer.Gen/Resources/Code/RoSpanReaderHelpers.cs`
- Added missing `ReadSByte` method (line 17-21)

### 6. Enabled Generated Source Output
- **File**: `tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj`
- Added `EmitCompilerGeneratedFiles` and `CompilerGeneratedFilesOutputPath`
- Generated files now in `tests/FourSer.Tests.Behavioural/Generated/`

## Current Test Status

```
Passed:  15/19
Failed:   4/19
```

### Passing Tests
- All `BatchTestAllPrimitives` tests (Span and Stream)
- `BatchTestMixedWithCollection` tests
- `BatchTestTrailingCollection` tests
- `BatchTestSinglePrimitive` tests
- `BatchTestExactThreshold` tests
- `BatchTestBelowThreshold` tests
- `BatchTestManyBytes` tests
- `BatchTestWithBooleans` tests
- `BatchTestNestedType` tests
- Edge case tests (empty collection, zero values)

### Failing Tests (Pre-existing Bug)
All failures are related to **string handling in Stream deserialization**:

1. `BatchTestMixedWithString_RoundTrip_Stream`
2. `BatchTestMixedWithString_RoundTrip_Span`
3. `BatchTestMixedWithString_Stream_Equals_Span`
4. `BatchTestLeadingString_RoundTrip_Stream`
5. `BatchTestMultipleStrings_RoundTrip_Stream`
6. `BatchTestMixedWithString_EmptyString_RoundTrip`

**Error Pattern**:
```
Expected: "Hello, World!"
Actual:   "\0\0Hello, Worl"
```

The string is reading with wrong offset - the length prefix (2 bytes) is being read as content.

## Bug Analysis

This appears to be a **pre-existing bug** in the serialization generator, not related to our batching implementation. The issue:

1. When serializing: `Before1(4) + Before2(4) + StringLength(2) + StringContent(N) + After1(4) + After2(4)`
2. When deserializing Stream: The string reader is somehow offset by 2 bytes

**To investigate**: Look at the generated code for `BatchTestMixedWithString`:
```
tests/FourSer.Tests.Behavioural/Generated/FourSer.Gen/FourSer.Gen.SerializerGenerator/
```

## Next Steps

### Immediate (Bug Fix)
1. View generated code for `BatchTestMixedWithString` to understand the bug
2. Compare Span vs Stream deserialization code
3. Fix the string reading offset issue

### After Bug Fix
1. Implement batching in `DeserializationGenerator.GenerateDeserializeWithStream()`
2. Modify to use `BatchingUtilities.AnalyzeMembers()` 
3. Generate batch read code for `BatchGroup` instructions
4. Run all tests to verify no regressions

### Implementation Location
- **File to modify**: `src/FourSer.Gen/CodeGenerators/DeserializationGenerator.cs`
- **Method**: `GenerateDeserializeWithStream()` (line ~30)

### Example Generated Code (Target)
```csharp
// Current (non-batched)
var startAct = StreamReader.ReadByte(stream);
var slot = StreamReader.ReadByte(stream);
// ... 13 more individual reads

// Target (batched)
Span<byte> batch0 = stackalloc byte[26];
stream.ReadExactly(batch0);
var startAct = batch0[0];
var slot = batch0[1];
// ... parse from memory
```

## Files Modified/Created

| File | Status |
|------|--------|
| `docs/instruction-batching-design.md` | Created |
| `docs/batching-implementation-progress.md` | Created |
| `src/FourSer.Gen/CodeGenerators/Core/BatchingUtilities.cs` | Created |
| `src/FourSer.Gen/Resources/Code/RoSpanReaderHelpers.cs` | Modified (added ReadSByte) |
| `tests/FourSer.Tests.Behavioural/Batching/BatchTestTypes.cs` | Created |
| `tests/FourSer.Tests.Behavioural/Batching/BatchingTests.cs` | Created |
| `tests/FourSer.Tests.Behavioural/FourSer.Tests.Behavioural.csproj` | Modified (emit generated) |

## Commands Reference

```powershell
# Build tests
dotnet build tests/FourSer.Tests.Behavioural

# Run all behavioural tests
dotnet test tests/FourSer.Tests.Behavioural

# Run only batching tests
dotnet test tests/FourSer.Tests.Behavioural --filter "FullyQualifiedName~BatchingTests"

# Run specific test
dotnet test tests/FourSer.Tests.Behavioural --filter "FullyQualifiedName~BatchTestMixedWithString_RoundTrip_Stream"

# View generated files
ls tests/FourSer.Tests.Behavioural/Generated/FourSer.Gen/FourSer.Gen.SerializerGenerator/
```
