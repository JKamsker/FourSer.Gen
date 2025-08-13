# Design Document

## Overview

This design addresses two critical bugs in the source generator's collection serialization logic:

1. **Count Type Bug**: The generator ignores the `CountType` specified in `[SerializeCollection(CountType = typeof(byte))]` and defaults to `int32` instead of using the specified type.

2. **Unnecessary Polymorphic Logic Bug**: The generator incorrectly assumes polymorphic serialization is needed for simple collections of concrete types, generating switch statements and throwing "Unknown type" exceptions when no polymorphic configuration is present.

The root cause analysis shows that the collection serialization logic in the code generators doesn't properly utilize the parsed `CollectionInfo.CountType` and incorrectly triggers polymorphic code paths for non-polymorphic scenarios.

## Architecture

The fix involves modifications to three main code generators:

- **SerializationGenerator**: Fix count type usage and polymorphic logic detection
- **DeserializationGenerator**: Fix count type usage and polymorphic logic detection  
- **PacketSizeGenerator**: Fix count type size calculation

The existing parsing logic in `TypeInfoProvider` and `AttributeHelper` correctly extracts the `CountType` from attributes, so no changes are needed there.

## Components and Interfaces

### 1. TypeHelper Enhancements

**Current Issue**: The `TypeHelper.GetDefaultCountType()` method returns "int" but the generators don't properly use the `CollectionInfo.CountType` when available.

**Solution**: Modify the code generators to use `member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType()` consistently.

### 2. SerializationGenerator Fixes

**Current Issue**: 
```csharp
// Bug: Always uses int32 regardless of CountType
var typeId = data.ReadInt32();
```

**Solution**:
```csharp
// Fixed: Use the specified CountType
var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
data.{countWriteMethod}(({countType})obj.{member.Name}.Count);
```

### 3. DeserializationGenerator Fixes

**Current Issue**:
```csharp
// Bug: Always reads int32 regardless of CountType
var typeId = data.ReadInt32();
```

**Solution**:
```csharp
// Fixed: Use the specified CountType
var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
var countReadMethod = TypeHelper.GetReadMethodName(countType);
var {member.Name}Count = data.{countReadMethod}();
```

### 4. PacketSizeGenerator Fixes

**Current Issue**:
```csharp
// Bug: Always uses sizeof(int) regardless of CountType
size += sizeof(int);
```

**Solution**:
```csharp
// Fixed: Use the specified CountType
var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
size += {countSizeExpression};
```

### 5. Polymorphic Logic Detection

**Current Issue**: The generators check for `member.PolymorphicInfo is not null` but this can be populated even when no polymorphic behavior is intended.

**Solution**: Add proper polymorphic mode detection:

```csharp
private static bool ShouldUsePolymorphicSerialization(MemberToGenerate member)
{
    // Only use polymorphic logic if explicitly configured
    if (member.CollectionInfo?.PolymorphicMode != PolymorphicMode.None)
        return true;
        
    // Or if SerializePolymorphic attribute is present with options
    if (member.PolymorphicInfo?.Options.Length > 0)
        return true;
        
    return false;
}
```

## Data Models

No changes needed to the existing data models. The `CollectionInfo` struct already contains the `CountType` field:

```csharp
public readonly record struct CollectionInfo(
    PolymorphicMode PolymorphicMode,
    string? TypeIdProperty,
    string? CountType,  // ← This field exists and is populated correctly
    int? CountSize,
    string? CountSizeReference) : IEquatable<CollectionInfo>;
```

## Error Handling

### Current Error Handling Issues

1. **False Positive Errors**: Collections of concrete types throw "Unknown type" exceptions
2. **Misleading Error Messages**: Errors suggest polymorphic configuration is needed when it's not

### Improved Error Handling

1. **Concrete Type Collections**: No polymorphic switch statements or exceptions
2. **True Polymorphic Collections**: Clear error messages when configuration is incomplete
3. **Count Type Validation**: Ensure CountType is a valid numeric type during generation

## Testing Strategy

### Unit Test Cases

1. **Count Type Tests**:
   - `[SerializeCollection(CountType = typeof(byte))]` → uses 1 byte for count
   - `[SerializeCollection(CountType = typeof(ushort))]` → uses 2 bytes for count
   - `[SerializeCollection(CountType = typeof(uint))]` → uses 4 bytes for count
   - `[SerializeCollection(CountType = typeof(ulong))]` → uses 8 bytes for count

2. **Non-Polymorphic Collection Tests**:
   - `List<Cat>` where `Cat : Animal` → direct serialization, no switch statements
   - `List<ConcreteType>` → direct serialization, no exceptions

3. **Polymorphic Collection Tests**:
   - `[SerializeCollection(PolymorphicMode = IndividualTypeIds)]` → generates switch statements
   - Missing `[PolymorphicOption]` attributes → compilation errors

### Integration Test Cases

1. **Round-trip Serialization**: Serialize and deserialize collections with custom count types
2. **Binary Compatibility**: Ensure byte-level compatibility with expected formats
3. **Performance**: Verify no performance regression from the fixes

### Test Data Scenarios

```csharp
// Test Case 1: Custom count type (should work)
[GenerateSerializer]
public partial class ByteCountTest
{
    [SerializeCollection(CountType = typeof(byte))]
    public List<Cat> Cats { get; set; } = new();
}

// Test Case 2: Concrete collection (should work without polymorphic logic)
[GenerateSerializer] 
public partial class ConcreteCollectionTest
{
    [SerializeCollection]
    public List<SimpleType> Items { get; set; } = new();
}

// Test Case 3: True polymorphic collection (should generate switch statements)
[GenerateSerializer]
public partial class PolymorphicCollectionTest
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)]
    [PolymorphicOption(1, typeof(Cat))]
    [PolymorphicOption(2, typeof(Dog))]
    public List<Animal> Animals { get; set; } = new();
}
```

## Implementation Approach

### Phase 1: Count Type Fixes
1. Update `SerializationGenerator.GenerateCollectionSerialization()` to use `CollectionInfo.CountType`
2. Update `DeserializationGenerator.GenerateCollectionDeserialization()` to use `CollectionInfo.CountType`
3. Update `PacketSizeGenerator.GenerateCollectionSizeCalculation()` to use `CollectionInfo.CountType`

### Phase 2: Polymorphic Logic Detection
1. Add `ShouldUsePolymorphicSerialization()` helper method
2. Update all three generators to use this method instead of checking `PolymorphicInfo is not null`
3. Remove polymorphic switch statements for simple concrete collections

### Phase 3: Testing and Validation
1. Add comprehensive test cases for both bug scenarios
2. Verify existing functionality is not broken
3. Validate generated code produces correct binary output

## Backward Compatibility

These fixes are backward compatible:
- Existing code without `CountType` specified continues to use `int32` (default behavior)
- Existing polymorphic collections continue to work as before
- Only fixes broken scenarios, doesn't change working ones