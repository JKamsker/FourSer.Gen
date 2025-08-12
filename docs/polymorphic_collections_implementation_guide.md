# Implementation Guide: Polymorphic Collections

## 1. Overview

This document outlines the technical implementation plan for adding polymorphic collection serialization to the source generator. The goal is to enable the serialization of collections (e.g., `List<T>`, arrays) containing different derived types.

To maintain a clean and intuitive API, this feature will be integrated directly into the existing `[SerializeCollectionAttribute]`, avoiding the need for a separate `[SerializePolymorphicCollection]` attribute.

### Key Concepts

The implementation will support two distinct modes for handling type information in collections:

1.  **`PolymorphicMode.IndividualTypeIds` (Heterogeneous Collections):** Each element in the collection is prefixed with its own `TypeId`. This is ideal for lists containing a mix of different types (e.g., a `List<Animal>` with `Cat` and `Dog` objects).

    ```mermaid
    graph TD
        subgraph "Serialized Stream (IndividualTypeIds)"
            direction LR
            Count --> TypeId1 --> Element1 --> TypeId2 --> Element2 --> Etc
        end
    ```

2.  **`PolymorphicMode.SingleTypeId` (Homogeneous Collections):** A single `TypeId` is written once before the elements of the collection. This is efficient for lists where all elements are guaranteed to be of the same concrete type. The `TypeId` is determined by a separate property on the containing class.

    ```mermaid
    graph TD
        subgraph "Serialized Stream (SingleTypeId)"
            direction LR
            Count --> TypeId --> Element1 --> Element2 --> Etc
        end
    ```

---

## 2. Task Breakdown

### Phase 1: Update `Serializer.Contracts`

The first step is to update the contracts assembly with the necessary API changes.

#### **Task 1.1: Create `PolymorphicMode` Enum**

Create a new file `PolymorphicMode.cs` in the `Serializer.Contracts` project.

**File:** `src/Serializer.Contracts/PolymorphicMode.cs`
```csharp
namespace Serializer.Contracts;

/// <summary>
/// Specifies the serialization mode for a polymorphic collection.
/// </summary>
public enum PolymorphicMode
{
    /// <summary>
    /// The collection is not polymorphic.
    /// </summary>
    None,
    /// <summary>
    /// A single TypeId is written for the entire collection. All elements must be of the same type.
    /// The TypeId is determined by the property specified in `TypeIdProperty`.
    /// </summary>
    SingleTypeId,
    /// <summary>
    /// Each element in the collection is prefixed with its own TypeId.
    /// </summary>
    IndividualTypeIds
}
```

#### **Task 1.2: Enhance `SerializeCollectionAttribute`**

Modify the existing `SerializeCollectionAttribute.cs` to include properties for controlling polymorphic serialization.

**File:** `src/Serializer.Contracts/SerializeCollectionAttribute.cs`
```csharp
// ... existing using statements ...

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializeCollectionAttribute : Attribute
{
    // ... existing properties (CountType, CountSize, CountSizeReference) ...

    /// <summary>
    /// Gets or sets the polymorphic serialization mode for this collection.
    /// Use `SingleTypeId` for homogeneous collections and `IndividualTypeIds` for heterogeneous collections.
    /// Defaults to `None`.
    /// </summary>
    public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;

    /// <summary>
    /// Gets or sets the type of the TypeId discriminator (e.g., typeof(byte), typeof(ushort)).
    /// This is used for both `SingleTypeId` and `IndividualTypeIds` modes.
    /// Defaults to `int`.
    /// </summary>
    public Type? TypeIdType { get; set; }

    /// <summary>
    /// For `SingleTypeId` mode only. The name of the property on the containing class that holds the TypeId for all elements in the collection.
    /// </summary>
    public string? TypeIdProperty { get; set; }
}
```

---

### Phase 2: Update `Serializer.Generator`

With the contracts updated, the source generator must be taught how to interpret and act on these new properties.

#### **Task 2.1: Update `AttributeHelper.cs`**

Add helper methods to extract the new polymorphic properties from an `AttributeData` object corresponding to `SerializeCollectionAttribute`.

**File:** `src/Serializer.Generator/AttributeHelper.cs`
```csharp
// ... existing code ...
public static class AttributeHelper
{
    // ... existing methods ...

    public static int GetPolymorphicMode(AttributeData? collectionAttribute)
    {
        var polymorphicModeArg = collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "PolymorphicMode");
        
        // The enum value is returned as an int. 0=None, 1=SingleTypeId, 2=IndividualTypeIds
        return polymorphicModeArg?.Value.Value as int? ?? 0;
    }

    public static ITypeSymbol? GetTypeIdType(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdType")
            .Value.Value as ITypeSymbol;
    }

    public static string? GetTypeIdProperty(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdProperty")
            .Value.Value?.ToString();
    }
}
```

#### **Task 2.2: Update Code Generators**

The core logic resides in the `PacketSizeGenerator`, `SerializationGenerator`, and `DeserializationGenerator`. Each needs to be updated to handle the two new polymorphic modes.

**In `PacketSizeGenerator.cs`:**
- When processing a collection member, check its `PolymorphicMode`.
- **For `IndividualTypeIds`:** The size is the sum of `sizeof(CountType)` + `SUM(sizeof(TypeId) + item.GetPacketSize())` for each item in the list. This requires iterating over the collection at serialization time.
- **For `SingleTypeId`:** The size is `sizeof(CountType)` + `sizeof(TypeId)` + (`collection.Count * sizeof(ConcreteType)`). The concrete type is determined via the `TypeIdProperty`.

**In `SerializationGenerator.cs`:**
- When processing a collection member, check its `PolymorphicMode`.
- **For `IndividualTypeIds`:**
    1. Write the collection count.
    2. Loop through the collection.
    3. For each item, determine its concrete type, find the matching `[PolymorphicOption]` to get its `TypeId`.
    4. Write the `TypeId`.
    5. Call the static `Serialize` method for the item's concrete type.
- **For `SingleTypeId`:**
    1. Write the collection count.
    2. Read the `TypeId` from the `TypeIdProperty` on the containing object.
    3. Write this `TypeId` once.
    4. Loop through the collection and call the static `Serialize` method for each item.

**In `DeserializationGenerator.cs`:**
- When processing a collection member, check its `PolymorphicMode`.
- **For `IndividualTypeIds`:**
    1. Read the collection count.
    2. Start a loop for the count.
    3. Inside the loop, read the `TypeId`.
    4. Use a `switch` statement on the `TypeId` to determine the concrete type to deserialize.
    5. Call the static `Deserialize` method for that concrete type.
    6. Add the new object to the list.
- **For `SingleTypeId`:**
    1. Read the collection count.
    2. Read the single `TypeId`.
    3. Use a `switch` statement to determine the concrete type.
    4. Start a loop for the count, calling the same static `Deserialize` method for the determined type in each iteration.

#### **Task 2.3: Add Validation**

In `SerializerGenerator.cs` or `TypeAnalyzer.cs`, add diagnostics to prevent misuse:
- If `PolymorphicMode` is `SingleTypeId`, `TypeIdProperty` must be specified.
- If `PolymorphicMode` is `IndividualTypeIds` or `None`, `TypeIdProperty` must NOT be specified.
- The type of the `TypeIdProperty` must match the `TypeIdType` if specified, or `int` by default.

---

### Phase 3: Testing and Documentation

#### **Task 3.1: Create Test Use Cases**

In `Serializer.Consumer/UseCases`, add new test files to validate the implementation.
- **`IndividualTypeIdsTest.cs`**: A test with a heterogeneous list (e.g., `List<Animal>`).
- **`SingleTypeIdTest.cs`**: A test with a homogeneous list and a `TypeIdProperty`.

#### **Task 3.2: Update `README.md`**

Add a new "Polymorphic Collections" section to the main `README.md`. Include clear code examples for both `IndividualTypeIds` and `SingleTypeId` modes, explaining the configuration for each.