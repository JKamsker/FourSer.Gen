# Implementation Plan

- [x] 1. Add polymorphic detection helper method

  - Create `ShouldUsePolymorphicSerialization()` helper method in `SerializationGenerator` class
  - Method should return true only when `PolymorphicMode != None` or when `PolymorphicInfo` has actual options configured
  - Add unit tests to verify the logic correctly identifies polymorphic vs non-polymorphic scenarios
  - _Requirements: 2.1, 2.2, 3.1, 3.3_

- [x] 2. Fix count type usage in SerializationGenerator

  - Modify `GenerateCollectionSerialization()` method to use `member.CollectionInfo?.CountType` instead of defaulting to int
  - Update the count writing logic to use the correct write method based on the specified count type
  - Ensure the method handles null CountType by falling back to the default int type
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 3. Fix polymorphic logic detection in SerializationGenerator

  - Update `GenerateCollectionSerialization()` method to use the new `ShouldUsePolymorphicSerialization()` helper
  - Remove polymorphic switch statement generation for simple concrete collections
  - Ensure concrete collections directly call the element type's `Serialize()` method
  - _Requirements: 2.3, 2.4, 2.5_

- [x] 4. Fix count type usage in DeserializationGenerator

  - Modify `GenerateCollectionDeserialization()` method to use `member.CollectionInfo?.CountType` instead of defaulting to int
  - Update the count reading logic to use the correct read method based on the specified count type
  - Ensure the method handles null CountType by falling back to the default int type
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.6_

- [x] 5. Fix polymorphic logic detection in DeserializationGenerator

  - Update `GenerateCollectionDeserialization()` method to use the new `ShouldUsePolymorphicSerialization()` helper
  - Remove polymorphic switch statement generation for simple concrete collections
  - Ensure concrete collections directly call the element type's `Deserialize()` method
  - _Requirements: 2.3, 2.4, 2.5_

- [x] 6. Fix count type usage in PacketSizeGenerator

  - Modify `GenerateCollectionSizeCalculation()` method to use `member.CollectionInfo?.CountType` instead of defaulting to int
  - Update the size calculation to use the correct sizeof expression based on the specified count type
  - Ensure the method handles null CountType by falling back to the default int type
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 7. Fix polymorphic logic detection in PacketSizeGenerator

  - Update `GenerateCollectionSizeCalculation()` method to use the new `ShouldUsePolymorphicSerialization()` helper
  - Remove polymorphic size calculation logic for simple concrete collections
  - Ensure concrete collections calculate size by directly calling element type's `GetPacketSize()` method
  - _Requirements: 2.3, 2.4, 2.5_

- [x] 8. Add comprehensive test cases for count type functionality

  - Create test cases for `CountType = typeof(byte)` collections
  - Create test cases for `CountType = typeof(ushort)` collections
  - Create test cases for `CountType = typeof(uint)` collections
  - Create test cases for `CountType = typeof(ulong)` collections
  - Verify that each test case generates the correct read/write methods and sizeof expressions
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 9. Add comprehensive test cases for non-polymorphic collections

  - Create test case for simple `List<Cat>` collection without polymorphic attributes
  - Create test case for concrete type collections that inherit from base classes
  - Verify that generated code does not contain polymorphic switch statements
  - Verify that generated code directly calls concrete type serialization methods
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 10. Add test cases for true polymorphic collections

  - Create test case for `PolymorphicMode.IndividualTypeIds` with proper `[PolymorphicOption]` attributes
  - Create test case for `[SerializePolymorphic]` attribute with TypeIdType specified
  - Verify that polymorphic switch statements are generated only for these cases
  - Verify that proper TypeId handling is implemented according to specified TypeIdType
  - _Requirements: 3.1, 3.2, 3.4, 3.5_

- [x] 11. Validate and test the complete fix

  - Run all existing tests to ensure no regressions
  - Run the new test cases to verify both bugs are fixed
  - Test the specific failing case from the original issue: `[SerializeCollection(CountType = typeof(byte))] List<Cat> Cats`
  - Verify that the generated code matches expected output for both count type and polymorphic logic
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5_
