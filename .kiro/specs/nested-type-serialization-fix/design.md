# Design Document

## Overview

The current serializer source generator has a bug when handling nested reference types within collections. The issue occurs in the `GenerateSource` method where type references use only the simple type name (`typeArgument.Name`) instead of the fully qualified name. This causes compilation errors when the referenced type is a nested class.

The fix involves creating a helper method to determine the correct type reference string based on whether the type is nested and its relationship to the current class being generated.

## Architecture

The solution maintains the existing architecture of the `SerializerGenerator` class but adds a new helper method to resolve type names correctly. The fix is localized to the `GenerateSource` method and does not require changes to the overall generator pipeline.

### Current Architecture
- `Initialize`: Sets up the incremental generator pipeline
- `Execute`: Processes each type declaration with the `GenerateSerializerAttribute`
- `GenerateSource`: Creates the serialization code for each class
- `GetMemberType`: Helper to extract type information from symbols
- `ClassToGenerate`: Data structure holding class information

### Enhanced Architecture
- All existing components remain unchanged
- New helper method: `GetTypeReference` - determines the correct type reference string
- Modified `GenerateSource` method to use the new helper for type references

## Components and Interfaces

### New Helper Method: GetTypeReference

```csharp
private static string GetTypeReference(ITypeSymbol typeSymbol, INamedTypeSymbol containingType)
```

**Purpose**: Determines the correct string representation for referencing a type in generated code.

**Parameters**:
- `typeSymbol`: The type symbol to generate a reference for
- `containingType`: The type symbol of the class currently being generated

**Logic**:
1. Check if the type is nested within the current class being generated
2. If nested within current class, use `ContainingType.Name.TypeName` format
3. If nested within a different class, use fully qualified name
4. If not nested, use simple name as before

**Return**: String representation suitable for code generation

### Modified GenerateSource Method

The `GenerateSource` method will be updated in three locations where `typeArgument.Name` is currently used:

1. **GetPacketSize method**: In the foreach loop for collection items
2. **Deserialize method**: When calling `Deserialize` on collection items  
3. **Serialize method**: When calling `Serialize` on collection items

Each location will replace `typeArgument.Name` with `GetTypeReference(typeArgument, namedTypeSymbol)` where `namedTypeSymbol` is the current class being generated.

## Data Models

### ClassToGenerate
No changes required to the existing `ClassToGenerate` class. It already contains all necessary information.

### Type Resolution Context
The solution requires passing the current class context (`INamedTypeSymbol`) to the type reference resolution method to determine relative type relationships.

## Error Handling

### Compilation Errors
- **Before**: Generated code references `NestedEntity` which doesn't exist in scope
- **After**: Generated code references `TestWithListOfNestedReferenceTypes.NestedEntity` which is the correct fully qualified name

### Edge Cases
1. **Multiple nesting levels**: The solution handles arbitrary nesting depth by using `ToDisplayString()` when needed
2. **Cross-class nested types**: Types nested in different classes will use fully qualified names
3. **Generic nested types**: The solution works with generic nested types by leveraging Roslyn's type symbol system

### Backward Compatibility
- Non-nested types continue to use simple names as before
- Existing functionality remains unchanged
- No breaking changes to the generated API

## Testing Strategy

### Unit Testing Approach
1. **Test existing functionality**: Ensure non-nested types continue to work
2. **Test nested types**: Verify correct type reference generation for nested classes
3. **Test complex scenarios**: Multiple nesting levels, generic nested types
4. **Test compilation**: Ensure generated code compiles successfully

### Test Cases
1. **Simple nested class**: `TestWithListOfNestedReferenceTypes.NestedEntity`
2. **Multiple nesting levels**: `OuterClass.MiddleClass.InnerClass`
3. **Generic nested types**: `Container<T>.NestedType`
4. **Cross-class references**: Nested types from different containing classes
5. **Mixed scenarios**: Collections with both nested and non-nested types

### Integration Testing
- Build the entire solution to ensure no compilation errors
- Test serialization/deserialization of nested types
- Verify generated code matches expected patterns

### Regression Testing
- Ensure existing test cases continue to pass
- Verify no changes to generated code for non-nested scenarios
- Test all existing serialization features remain functional

## Implementation Notes

### Roslyn API Usage
- Use `ITypeSymbol.ContainingType` to detect nested types
- Use `ITypeSymbol.ToDisplayString()` for fully qualified names when needed
- Leverage existing symbol comparison methods for type relationship detection

### Code Generation Patterns
- Maintain consistent indentation and formatting
- Use the same variable naming conventions as existing code
- Preserve all existing comments and structure

### Performance Considerations
- The type reference resolution is O(1) for each type
- No significant performance impact on the generation process
- Memory usage remains the same as before