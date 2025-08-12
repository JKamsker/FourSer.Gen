# Implementation Plan

- [x] 1. Create helper method for type reference resolution

  - Add `GetTypeReference` method to `SerializerGenerator` class
  - Implement logic to detect nested types and generate correct type references
  - Handle edge cases for multiple nesting levels and cross-class references
  - _Requirements: 1.1, 2.1, 2.2, 2.3_

- [x] 2. Update GetPacketSize method generation for nested types

  - Replace `typeArgument.Name` with `GetTypeReference(typeArgument, namedTypeSymbol)` in collection handling
  - Ensure the generated code uses fully qualified names for nested types
  - Maintain existing behavior for non-nested types
  - _Requirements: 1.2, 3.1, 4.4_

- [x] 3. Update Deserialize method generation for nested types


  - Replace `typeArgument.Name` with `GetTypeReference(typeArgument, namedTypeSymbol)` in collection deserialization
  - Ensure proper type references in the generated Deserialize calls
  - Maintain existing behavior for non-nested types
  - _Requirements: 1.3, 3.2, 4.4_

- [ ] 4. Update Serialize method generation for nested types

  - Replace `typeArgument.Name` with `GetTypeReference(typeArgument, namedTypeSymbol)` in collection serialization
  - Ensure proper type references in the generated Serialize calls
  - Maintain existing behavior for non-nested types
  - _Requirements: 1.4, 3.3, 4.4_

- [ ] 5. Test the fix with TestWithListOfNestedReferenceTypes

  - Build the solution to verify the generated code compiles successfully
  - Verify that TestWithListOfNestedReferenceTypes generates correct code
  - Ensure the generated code uses `TestWithListOfNestedReferenceTypes.NestedEntity` references
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [ ] 6. Verify backward compatibility with existing functionality
  - Test that non-nested types continue to generate the same code as before
  - Ensure primitive types, strings, and unmanaged types work unchanged
  - Verify no regression in existing serialization functionality
  - _Requirements: 3.1, 3.2, 3.3, 3.4_
