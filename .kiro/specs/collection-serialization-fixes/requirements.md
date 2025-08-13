# Requirements Document

## Introduction

This feature addresses critical bugs in the source generator's collection serialization logic. The generator currently produces incorrect code when handling collections with custom count types and incorrectly assumes polymorphic serialization is needed for simple collection scenarios.

## Requirements

### Requirement 1

**User Story:** As a developer using the serializer, I want collections with custom count types to serialize correctly, so that my binary data uses the specified count type instead of defaulting to int32.

#### Acceptance Criteria

1. WHEN a collection is marked with `[SerializeCollection(CountType = typeof(byte))]` THEN the generator SHALL use byte (1 byte) for the count field instead of int32 (4 bytes)
2. WHEN a collection is marked with `[SerializeCollection(CountType = typeof(ushort))]` THEN the generator SHALL use ushort (2 bytes) for the count field
3. WHEN a collection is marked with `[SerializeCollection(CountType = typeof(uint))]` THEN the generator SHALL use uint (4 bytes) for the count field
4. WHEN a collection is marked with `[SerializeCollection(CountType = typeof(ulong))]` THEN the generator SHALL use ulong (8 bytes) for the count field
5. WHEN serializing a collection with custom count type THEN the count SHALL be written using the specified type's byte size
6. WHEN deserializing a collection with custom count type THEN the count SHALL be read using the specified type's byte size

### Requirement 2

**User Story:** As a developer using the serializer, I want simple collections of concrete types to generate straightforward serialization code, so that unnecessary polymorphic logic is not included.

#### Acceptance Criteria

1. WHEN a collection has no `PolymorphicMode` specified or `PolymorphicMode.None` THEN the generator SHALL NOT generate polymorphic switch statements
2. WHEN a collection contains concrete types without `[PolymorphicOption]` attributes THEN the generator SHALL treat elements as concrete types
3. WHEN serializing a simple collection like `List<Cat>` with `[SerializeCollection(CountType = typeof(byte))]` THEN the generator SHALL directly call `Cat.Serialize()` for each element
4. WHEN deserializing a simple collection like `List<Cat>` THEN the generator SHALL directly call `Cat.Deserialize()` for each element
5. WHEN a collection element type is concrete and has `[GenerateSerializer]` THEN the generator SHALL NOT throw "Unknown type" exceptions

### Requirement 3

**User Story:** As a developer using polymorphic collections, I want the generator to only include polymorphic logic when explicitly configured, so that I have full control over when polymorphic serialization is used.

#### Acceptance Criteria

1. WHEN a collection has `PolymorphicMode = PolymorphicMode.IndividualTypeIds` AND `[PolymorphicOption]` attributes THEN the generator SHALL include polymorphic switch statements
2. WHEN a collection has `[SerializePolymorphic]` attribute with `TypeIdType` specified THEN the generator SHALL generate polymorphic serialization logic
3. WHEN a collection has `PolymorphicMode.None` or no polymorphic attributes THEN the generator SHALL generate simple concrete type serialization
4. WHEN polymorphic configuration is incomplete (missing PolymorphicOption attributes) THEN the generator SHALL provide clear compilation errors
5. WHEN a collection is configured for polymorphism THEN the generator SHALL handle TypeId reading/writing according to the specified TypeIdType