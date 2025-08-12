# Requirements Document

## Introduction

The serializer source generator currently has a bug when handling nested reference types within collections. When a class contains a `[SerializeCollection]` property with nested class types, the generated code incorrectly references the nested type without its fully qualified name, causing compilation errors. This feature will fix the source generator to properly handle nested types in collections by using fully qualified type names.

## Requirements

### Requirement 1

**User Story:** As a developer using the serializer generator, I want nested classes within collections to be properly serialized and deserialized, so that my code compiles without errors and works correctly.

#### Acceptance Criteria

1. WHEN a class has a `[SerializeCollection]` property containing nested reference types THEN the generator SHALL use the fully qualified type name in the generated code
2. WHEN generating GetPacketSize method for nested types THEN the generator SHALL reference the nested type with its containing class name
3. WHEN generating Deserialize method for nested types THEN the generator SHALL reference the nested type with its containing class name  
4. WHEN generating Serialize method for nested types THEN the generator SHALL reference the nested type with its containing class name

### Requirement 2

**User Story:** As a developer, I want the source generator to handle both simple nested types and complex nested type hierarchies, so that I can use any level of nesting in my serializable classes.

#### Acceptance Criteria

1. WHEN a nested type is referenced in generated code THEN the generator SHALL determine if the type is nested within the current class
2. IF a type is nested within the current class THEN the generator SHALL use the containing class name as prefix
3. IF a type is nested within a different class THEN the generator SHALL use the fully qualified name including namespace and containing class
4. WHEN multiple levels of nesting exist THEN the generator SHALL handle the full hierarchy correctly

### Requirement 3

**User Story:** As a developer, I want existing functionality to remain unchanged while the nested type bug is fixed, so that my current working code continues to function properly.

#### Acceptance Criteria

1. WHEN processing non-nested types THEN the generator SHALL continue to work exactly as before
2. WHEN processing primitive types in collections THEN the generator SHALL maintain current behavior
3. WHEN processing string types THEN the generator SHALL maintain current behavior
4. WHEN processing unmanaged types THEN the generator SHALL maintain current behavior

### Requirement 4

**User Story:** As a developer, I want the TestWithListOfNestedReferenceTypes class to generate valid compilable code, so that my solution builds successfully without errors.

#### Acceptance Criteria

1. WHEN the source generator processes TestWithListOfNestedReferenceTypes THEN it SHALL generate syntactically correct C# code
2. WHEN the generated code is compiled THEN it SHALL build without compilation errors
3. WHEN the solution is built THEN TestWithListOfNestedReferenceTypes SHALL be properly serializable and deserializable
4. WHEN referencing NestedEntity in generated code THEN it SHALL use TestWithListOfNestedReferenceTypes.NestedEntity as the fully qualified name