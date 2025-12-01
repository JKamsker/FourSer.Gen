# Instruction Batching Design for Stream Deserialization

## Overview

This document outlines the design for **instruction batching** in FourSer.Gen's stream deserialization. The goal is to reduce the number of stream read calls by reading consecutive fixed-size fields in a single batch, then parsing them from memory.

## Problem Statement

Currently, the generated stream deserialization code reads each field individually:

```csharp
// Current: 16 separate ReadExactly calls for CharacterInfo
var charID = StreamReader.ReadUInt32(stream);      // 1 syscall (4 bytes)
var name = StreamReader.ReadString(stream);         // variable - conditional point
var startAct = StreamReader.ReadByte(stream);       // 1 syscall (1 byte)
var slot = StreamReader.ReadByte(stream);           // 1 syscall (1 byte)
var level = StreamReader.ReadByte(stream);          // 1 syscall (1 byte)
// ... 10 more individual byte reads ...
var region = StreamReader.ReadUInt32(stream);       // 1 syscall (4 bytes)
var fame = StreamReader.ReadUInt32(stream);         // 1 syscall (4 bytes)
var fameColor = StreamReader.ReadUInt32(stream);    // 1 syscall (4 bytes)
var itemsCount = StreamReader.ReadByte(stream);     // 1 syscall (1 byte)
// ... collection loop - conditional point
```

Each `ReadExactly` call potentially triggers a syscall. For `CharacterInfo`, this means **18+ syscalls** per object.

## Proposed Solution

Identify **batches** of consecutive unmanaged-type fields and read them in a single call:

```csharp
// Optimized: 4 syscalls total for CharacterInfo
var charID = StreamReader.ReadUInt32(stream);       // 1 syscall (4 bytes) - before string
var name = StreamReader.ReadString(stream);          // 1+ syscalls - variable length

// BATCH: 13 bytes + 12 bytes + 1 byte = 26 bytes
Span<byte> batch0 = stackalloc byte[26];
stream.ReadExactly(batch0);
var startAct = batch0[0];
var slot = batch0[1];
var level = batch0[2];
var classId = batch0[3];
var race = batch0[4];
var country = batch0[5];
var sex = batch0[6];
var hair = batch0[7];
var face = batch0[8];
var body = batch0[9];
var pants = batch0[10];
var hand = batch0[11];
var foot = batch0[12];
var region = BitConverter.ToUInt32(batch0.Slice(13, 4));
var fame = BitConverter.ToUInt32(batch0.Slice(17, 4));
var fameColor = BitConverter.ToUInt32(batch0.Slice(21, 4));
var itemsCount = batch0[25];

// Collection loop - conditional point
```

## Key Concepts

### Conditional Points

A **conditional point** is where the number of bytes needed depends on data already read:

1. **String fields** - Length is encoded in the first 2 bytes (ushort), variable content
2. **Collection fields** - Count is read first, then variable number of elements
3. **Polymorphic fields** - Type ID determines which concrete type to deserialize
4. **Nested serializable types** - May contain any of the above internally

### Batchable Fields

Fields that have a **fixed, known size at compile time**:

- Primitive unmanaged types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `bool`, `decimal`
- Enums (backed by primitives)
- Fixed-size structs (future enhancement)

### Batch Detection Algorithm

```
FOR each member in members:
  IF member is batchable (unmanaged type, not string/collection/polymorphic/nested serializable):
    Add to current batch with its size
  ELSE:
    IF current batch has >= MIN_BATCH_SIZE bytes:
      Emit batch read
      Emit member extractions from batch
    Emit standard read for non-batchable member
    Start new batch
    
IF remaining batch has >= MIN_BATCH_SIZE bytes:
  Emit batch read
```

### Minimum Batch Threshold

Only batch when cumulative size exceeds a threshold (e.g., 8 bytes). A single `int` read isn't worth batching, but `int + int + int` (12 bytes) is.

Configurable via:
- Global default: 8 bytes
- Per-type attribute: `[GenerateSerializer(BatchMinSize = 16)]`

## Implementation Plan

### Phase 1: Core Infrastructure

#### 1.1 Member Analysis (`MemberBatchInfo`)

Add a new model to track batching information:

```csharp
// In Models/BatchModels.cs
public readonly record struct MemberBatchInfo(
    int StartOffset,      // Offset within batch
    int Size,             // Size in bytes
    string TypeName,      // Original type name
    string ReadExpression // e.g., "batch0[0]" or "BitConverter.ToUInt32(batch0.Slice(4, 4))"
);

public readonly record struct BatchInfo(
    int TotalSize,
    EquatableArray<MemberBatchInfo> Members
);
```

#### 1.2 Batch Detection Utility

```csharp
// In CodeGenerators/Core/BatchingUtilities.cs
public static class BatchingUtilities
{
    public const int DefaultMinBatchSize = 8;

    public static List<object> AnalyzeMembers(
        IEnumerable<MemberToGenerate> members,
        TypeToGenerate type,
        int minBatchSize = DefaultMinBatchSize)
    {
        // Returns mixed list of:
        // - BatchInfo (for batched reads)
        // - MemberToGenerate (for non-batchable fields)
    }

    public static bool IsBatchable(MemberToGenerate member, TypeToGenerate type)
    {
        // Must be unmanaged type
        if (!member.IsUnmanagedType) return false;
        
        // Must not be string, collection, polymorphic, or have custom serializer
        if (member.IsStringType) return false;
        if (member.IsList || member.IsCollection) return false;
        if (member.PolymorphicInfo is not null) return false;
        if (member.HasGenerateSerializerAttribute) return false;
        if (GeneratorUtilities.ResolveSerializer(member, type) is not null) return false;
        
        return true;
    }

    public static int GetMemberSize(MemberToGenerate member)
    {
        return TypeHelper.GetSizeOf(member.TypeName);
    }
}
```

### Phase 2: Stream Deserialization Generator

Modify `DeserializationGenerator.GenerateDeserializeWithStream`:

```csharp
private static void GenerateDeserializeWithStream(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
{
    var instructions = BatchingUtilities.AnalyzeMembers(
        typeToGenerate.Members, 
        typeToGenerate, 
        minBatchSize: 8);
    
    int batchIndex = 0;
    
    foreach (var instruction in instructions)
    {
        if (instruction is BatchInfo batch)
        {
            GenerateBatchRead(sb, batch, batchIndex++);
        }
        else if (instruction is MemberToGenerate member)
        {
            GenerateMemberDeserialization(sb, member, typeToGenerate, true, "stream", "StreamReader");
        }
    }
    
    // ... constructor call logic unchanged
}

private static void GenerateBatchRead(IndentedStringBuilder sb, BatchInfo batch, int batchIndex)
{
    var batchVarName = $"batch{batchIndex}";
    
    // Use stackalloc for small batches, ArrayPool for larger ones
    if (batch.TotalSize <= 256)
    {
        sb.WriteLineFormat("Span<byte> {0} = stackalloc byte[{1}];", batchVarName, batch.TotalSize);
    }
    else
    {
        sb.WriteLineFormat("var {0}Rented = System.Buffers.ArrayPool<byte>.Shared.Rent({1});", batchVarName, batch.TotalSize);
        sb.WriteLineFormat("var {0} = {0}Rented.AsSpan(0, {1});", batchVarName, batch.TotalSize);
    }
    
    sb.WriteLineFormat("stream.ReadExactly({0});", batchVarName);
    
    foreach (var memberInfo in batch.Members)
    {
        var varName = memberInfo.MemberName.ToCamelCase();
        sb.WriteLineFormat("var {0} = {1};", varName, memberInfo.ReadExpression);
    }
    
    if (batch.TotalSize > 256)
    {
        sb.WriteLineFormat("System.Buffers.ArrayPool<byte>.Shared.Return({0}Rented);", batchVarName);
    }
}
```

### Phase 3: Expression Generation

Helper to generate the extraction expression from batch:

```csharp
public static string GetBatchReadExpression(string batchVar, string typeName, int offset, int size)
{
    return typeName switch
    {
        "byte" => $"{batchVar}[{offset}]",
        "sbyte" => $"(sbyte){batchVar}[{offset}]",
        "bool" => $"{batchVar}[{offset}] != 0",
        "short" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian({batchVar}.Slice({offset}))",
        "ushort" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian({batchVar}.Slice({offset}))",
        "int" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({batchVar}.Slice({offset}))",
        "uint" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian({batchVar}.Slice({offset}))",
        "long" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian({batchVar}.Slice({offset}))",
        "ulong" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian({batchVar}.Slice({offset}))",
        "float" => $"System.BitConverter.ToSingle({batchVar}.Slice({offset}))",
        "double" => $"System.BitConverter.ToDouble({batchVar}.Slice({offset}))",
        "decimal" => GenerateDecimalRead(batchVar, offset), // Special case: 16 bytes
        _ => throw new NotSupportedException($"Unsupported batch type: {typeName}")
    };
}
```

## Generated Code Example

### Before (CharacterInfo)

```csharp
public static CharacterInfo Deserialize(System.IO.Stream stream)
{
    var charID = StreamReader.ReadUInt32(stream);
    var name = StreamReader.ReadString(stream);
    var startAct = StreamReader.ReadByte(stream);
    var slot = StreamReader.ReadByte(stream);
    var level = StreamReader.ReadByte(stream);
    var classId = StreamReader.ReadByte(stream);
    var race = StreamReader.ReadByte(stream);
    var country = StreamReader.ReadByte(stream);
    var sex = StreamReader.ReadByte(stream);
    var hair = StreamReader.ReadByte(stream);
    var face = StreamReader.ReadByte(stream);
    var body = StreamReader.ReadByte(stream);
    var pants = StreamReader.ReadByte(stream);
    var hand = StreamReader.ReadByte(stream);
    var foot = StreamReader.ReadByte(stream);
    var region = StreamReader.ReadUInt32(stream);
    var fame = StreamReader.ReadUInt32(stream);
    var fameColor = StreamReader.ReadUInt32(stream);
    var itemsCount = StreamReader.ReadByte(stream);
    var items = new List<ItemInfo>(itemsCount);
    for (int i = 0; i < itemsCount; i++)
    {
        items.Add(ItemInfo.Deserialize(stream));
    }
    return new CharacterInfo(charID, name, startAct, slot, level, classId, ...);
}
```

### After (CharacterInfo)

```csharp
public static CharacterInfo Deserialize(System.IO.Stream stream)
{
    var charID = StreamReader.ReadUInt32(stream);
    var name = StreamReader.ReadString(stream);
    
    // Batch read: 13 bytes + 12 bytes + 1 byte = 26 bytes
    Span<byte> batch0 = stackalloc byte[26];
    stream.ReadExactly(batch0);
    var startAct = batch0[0];
    var slot = batch0[1];
    var level = batch0[2];
    var classId = batch0[3];
    var race = batch0[4];
    var country = batch0[5];
    var sex = batch0[6];
    var hair = batch0[7];
    var face = batch0[8];
    var body = batch0[9];
    var pants = batch0[10];
    var hand = batch0[11];
    var foot = batch0[12];
    var region = BinaryPrimitives.ReadUInt32LittleEndian(batch0.Slice(13));
    var fame = BinaryPrimitives.ReadUInt32LittleEndian(batch0.Slice(17));
    var fameColor = BinaryPrimitives.ReadUInt32LittleEndian(batch0.Slice(21));
    var itemsCount = batch0[25];
    
    var items = new List<ItemInfo>(itemsCount);
    for (int i = 0; i < itemsCount; i++)
    {
        items.Add(ItemInfo.Deserialize(stream));
    }
    return new CharacterInfo(charID, name, startAct, slot, level, classId, ...);
}
```

## String Length Prefetch Optimization (Advanced)

For patterns like `int, int, string`, we can optimize further:

```csharp
// Read 8 bytes (int, int) + 2 bytes (string length) = 10 bytes
Span<byte> batch0 = stackalloc byte[10];
stream.ReadExactly(batch0);
var field1 = BinaryPrimitives.ReadInt32LittleEndian(batch0);
var field2 = BinaryPrimitives.ReadInt32LittleEndian(batch0.Slice(4));
var stringLength = BinaryPrimitives.ReadUInt16LittleEndian(batch0.Slice(8));

// Now read string content
Span<byte> stringBytes = stringLength <= 512 ? stackalloc byte[stringLength] : ...;
stream.ReadExactly(stringBytes);
var myString = Encoding.UTF8.GetString(stringBytes);
```

This requires modifying the string reading logic to accept a pre-read length.

## Serialization (Writing) Batching

Similar optimization applies to writing:

```csharp
// Before
StreamWriter.WriteByte(stream, obj.StartAct);
StreamWriter.WriteByte(stream, obj.Slot);
// ... 13 more WriteByte calls
StreamWriter.WriteUInt32(stream, obj.Region);
// ...

// After
Span<byte> batch0 = stackalloc byte[26];
batch0[0] = obj.StartAct;
batch0[1] = obj.Slot;
// ...
BinaryPrimitives.WriteUInt32LittleEndian(batch0.Slice(13), obj.Region);
// ...
stream.Write(batch0);
```

## Configuration Options

### Attribute-Based Control

```csharp
[GenerateSerializer(EnableBatching = true, BatchMinSize = 8)]
public partial class MyPacket
{
    public int Field1 { get; set; }
    public int Field2 { get; set; }
    
    [NoBatch] // Opt-out specific field from batching
    public int Field3 { get; set; }
}
```

### Global Configuration

Via `Directory.Build.props` or `.editorconfig`:

```xml
<PropertyGroup>
  <FourSerEnableBatching>true</FourSerEnableBatching>
  <FourSerBatchMinSize>8</FourSerBatchMinSize>
</PropertyGroup>
```

## Edge Cases

1. **Single field types** - If only one unmanaged field exists between conditional points, don't batch
2. **Struct alignment** - Ensure generated code respects alignment requirements
3. **Endianness** - Use `BinaryPrimitives` for consistent little-endian behavior
4. **ArrayPool cleanup** - Ensure proper return in all code paths (try/finally for large batches)

## Performance Impact

| Scenario | Before | After | Improvement |
|----------|--------|-------|-------------|
| CharacterInfo (26 fixed bytes) | 18 syscalls | 4 syscalls | ~77% reduction |
| Simple packet (12 fixed bytes) | 4 syscalls | 1 syscall | ~75% reduction |
| String-heavy packet | Variable | Variable | Minimal (strings dominate) |

## Files to Modify

1. `Models/Models.cs` - Add `BatchInfo`, `MemberBatchInfo`
2. `CodeGenerators/Core/BatchingUtilities.cs` - New file for batch analysis
3. `CodeGenerators/DeserializationGenerator.cs` - Stream method batching
4. `CodeGenerators/SerializationGenerator.cs` - Stream method batching (optional)
5. `Helpers/TypeHelper.cs` - Add `GetSizeOf` helper (if not present)

## Testing Strategy

> **CRITICAL**: Extensive testing MUST be completed and passing BEFORE merging any batching implementation. This feature touches core serialization logic—any bugs could cause silent data corruption.

### Test-First Development Approach

1. **Baseline tests FIRST** - Before implementing batching, create comprehensive tests that verify current (non-batched) behavior
2. **Bit-for-bit verification** - Batched output must be identical to non-batched output
3. **Round-trip validation** - Serialize → Deserialize must produce identical objects
4. **No regressions** - All existing tests must continue to pass

### Required Test Coverage

#### 1. Unit Tests (`BatchingUtilities`)

```csharp
[Test] void AnalyzeMembers_AllUnmanaged_SingleBatch()
[Test] void AnalyzeMembers_StringInMiddle_TwoBatches()
[Test] void AnalyzeMembers_CollectionBreaksBatch()
[Test] void AnalyzeMembers_PolymorphicBreaksBatch()
[Test] void AnalyzeMembers_NestedSerializableBreaksBatch()
[Test] void AnalyzeMembers_BelowMinSize_NoBatch()
[Test] void AnalyzeMembers_ExactlyMinSize_CreatesBatch()
[Test] void IsBatchable_UnmanagedType_ReturnsTrue()
[Test] void IsBatchable_StringType_ReturnsFalse()
[Test] void IsBatchable_CustomSerializer_ReturnsFalse()
[Test] void GetBatchReadExpression_AllPrimitiveTypes()
```

#### 2. Integration Tests (Generated Code Correctness)

```csharp
// For each test type, verify:
[Test] void Batched_Stream_ProducesSameResult_As_NonBatched()
[Test] void Batched_Deserialize_RoundTrips_Correctly()
[Test] void Batched_Output_ByteForByte_Identical()
```

Test types to cover:
- `SimpleAllPrimitives` - Only unmanaged types (ideal batching case)
- `MixedWithStrings` - Primitives interrupted by strings
- `MixedWithCollections` - Primitives interrupted by collections
- `LeadingString` - String first, then primitives
- `TrailingCollection` - Primitives, then collection at end
- `NestedTypes` - Types containing other `[GenerateSerializer]` types
- `PolymorphicMembers` - Types with polymorphic fields
- `SinglePrimitive` - Edge case: only one field (should NOT batch)
- `EmptyType` - Edge case: no fields

#### 3. Fuzz Testing

```csharp
[Test]
[Repeat(10000)]
void FuzzTest_RandomData_DeserializesWithoutCrash()
{
    var randomBytes = GenerateRandomBytes(1, 1024);
    // Should either deserialize or throw clean exception, never corrupt
}

[Test]
[Repeat(1000)]
void FuzzTest_ValidObject_RoundTrips()
{
    var obj = GenerateRandomValidObject<CharacterInfo>();
    var serialized = Serialize(obj);
    var deserialized = Deserialize(serialized);
    Assert.AreEqual(obj, deserialized);
}
```

#### 4. Edge Case Tests

```csharp
[Test] void Batch_AtExactly256Bytes_UsesStackalloc()
[Test] void Batch_Above256Bytes_UsesArrayPool()
[Test] void Batch_ArrayPoolReturned_OnSuccess()
[Test] void Batch_ArrayPoolReturned_OnException()
[Test] void Batch_EmptyStream_ThrowsEndOfStream()
[Test] void Batch_PartialData_ThrowsEndOfStream()
[Test] void Batch_DecimalType_CorrectLayout()
[Test] void Batch_MixedEndianness_Consistent() // If supporting big-endian
```

#### 5. Benchmark Tests (Performance Validation)

```csharp
[Benchmark] void Baseline_NonBatched_CharacterInfo()
[Benchmark] void Batched_CharacterInfo()
[Benchmark] void Baseline_NonBatched_SimplePacket()
[Benchmark] void Batched_SimplePacket()
```

Acceptance criteria:
- Batched version must be **faster or equal** (never slower)
- Memory allocations must be **equal or lower**

#### 6. Regression Tests

- All existing tests in `tests/` folder must pass unchanged
- Run full test suite before and after implementation
- Compare generated code output for all existing types

### Test Execution Gates

| Gate | Requirement |
|------|-------------|
| PR Merge | 100% of unit tests passing |
| PR Merge | 100% of integration tests passing |
| PR Merge | All existing tests passing (no regressions) |
| PR Merge | Benchmark shows no performance regression |
| Release | Fuzz testing completed (10,000+ iterations) |

### Test Type Definitions

Create dedicated test types in `tests/FourSer.Tests/BatchingTestTypes/`:

```csharp
[GenerateSerializer]
public partial class BatchTestAllPrimitives
{
    public byte B1 { get; set; }
    public short S1 { get; set; }
    public int I1 { get; set; }
    public long L1 { get; set; }
    public float F1 { get; set; }
    public double D1 { get; set; }
}

[GenerateSerializer]
public partial class BatchTestMixedWithString
{
    public int Before1 { get; set; }
    public int Before2 { get; set; }
    public string Middle { get; set; }
    public int After1 { get; set; }
    public int After2 { get; set; }
}

// ... more test types
```

## Future Enhancements

1. **Async batching** - `ReadExactlyAsync` for async stream reading
2. **Fixed-size struct batching** - Batch `[StructLayout(LayoutKind.Sequential)]` types
3. **Intelligent string prefetch** - Read length prefix in previous batch
4. **SIMD parsing** - Use `Vector<byte>` for ultra-high-throughput scenarios
