# Refactoring Plan: Member Reference Resolution

## 1. Overview

The goal of this refactoring is to improve the source generation process by resolving member references and calculating type sizes during the initial analysis phase within the `TypeInfoProvider`. Currently, this logic is handled during the code generation phase using dictionaries, which is less efficient and makes the code harder to maintain.

By enriching the `TypeToGenerate` data model with this information upfront, we can simplify the code generators, making them cleaner, more robust, and easier to understand.

## 2. Workflow Diagram

This diagram illustrates the proposed change in the data flow:

```mermaid
graph TD
    subgraph TypeInfoProvider [Phase 1: Analysis]
        A[GetSemanticTargetForGeneration] --> B[GetSerializableMembers as strings];
        B --> C[Create Name-to-Index Map];
        C --> D[Iterate Members & Resolve String Refs to Indices & Calculate Type Sizes];
        D --> E[Create new MemberToGenerate with resolved Index and Sizes];
        E --> F[Create final TypeToGenerate model];
    end

    subgraph CodeGenerators [Phase 2: Code Generation]
        G[Generator e.g., SerializationGenerator] --> H[Receives enriched TypeToGenerate model];
        H --> I[Access referenced member via index and use pre-calculated size];
        I --> J[typeToGenerate.Members[theIndex] and member.CollectionInfo.CountTypeSizeInBytes];
    end

    F --> G;

    style TypeInfoProvider fill:#cde4f9,stroke:#8ab8e6,stroke-width:2px
    style CodeGenerators fill:#d5f0d5,stroke:#93c493,stroke-width:2px
```

## 3. Implementation Steps

Here is a detailed breakdown of the required changes:

### Step 1: Enhance Data Models

**File:** `src/FourSer.Gen/Models/Models.cs`

- **Objective:** Update the `CollectionInfo` and `PolymorphicInfo` records to store pre-calculated data.

- **Changes:**
    - Modify the `CollectionInfo` record to include:
        - `int? CountSizeReferenceIndex`: Will store the index of the member referenced by `CountSizeReference`.
        - `int? CountTypeSizeInBytes`: Will store the byte size of the `CountType`.
    - Modify the `PolymorphicInfo` record to include:
        - `int? TypeIdPropertyIndex`: Will store the index of the member referenced by `TypeIdProperty`.
        - `int? TypeIdSizeInBytes`: Will store the byte size of the `TypeIdType`.

### Step 2: Enhance `TypeInfoProvider`

**File:** `src/FourSer.Gen/TypeInfoProvider.cs`

- **Objective:** Implement the logic to resolve references and calculate sizes during type analysis.

- **Changes:**
    1.  After the `serializableMembers` list is finalized, create a `Dictionary<string, int>` mapping member names to their index in the list.
    2.  Iterate through the `serializableMembers`. For each member:
        - If `CollectionInfo.CountSizeReference` is not null, use the dictionary to find the index of the referenced member and populate the new `CountSizeReferenceIndex` property.
        - If `PolymorphicInfo.TypeIdProperty` is not null, use the dictionary to find the index of the referenced member and populate the new `TypeIdPropertyIndex` property.
        - Calculate the byte size for `CollectionInfo.CountType` and `PolymorphicInfo.TypeIdType` and populate the corresponding `...SizeInBytes` properties.
    3.  Ensure the updated `CollectionInfo` and `PolymorphicInfo` objects are used when creating the final `MemberToGenerate` instances.

### Step 3: Refactor `SerializerGenerator`

**File:** `src/FourSer.Gen/SerializerGenerator.cs`

- **Objective:** Remove the obsolete dictionary-based logic.

- **Changes:**
    - Delete the `countRefMap` and `typeIdRefMap` dictionaries.
    - Remove the code that populates these dictionaries within the `Execute` method.

### Step 4: Update `PacketSizeGenerator`

**File:** `src/FourSer.Gen/CodeGenerators/PacketSizeGenerator.cs`

- **Objective:** Adapt the generator to use the new pre-calculated data.

- **Changes:**
    - Replace checks like `string.IsNullOrEmpty(collectionInfo.CountSizeReference)` with checks on the new `CountSizeReferenceIndex` property.
    - Instead of calculating the size of the count type on the fly, use the pre-calculated `CountTypeSizeInBytes` property.
    - Do the same for the polymorphic type ID, using `TypeIdPropertyIndex` and `TypeIdSizeInBytes`.

### Step 5: Update `SerializationGenerator`

**File:** `src/FourSer.Gen/CodeGenerators/SerializationGenerator.cs`

- **Objective:** Adapt the generator to use the new pre-calculated data.

- **Changes:**
    - Update all logic that previously used `countRefMap` and `typeIdRefMap`.
    - Access referenced members directly via the `...Index` properties (e.g., `typeToGenerate.Members[index]`).
    - Use the pre-calculated `...SizeInBytes` properties when writing count and type ID values.

### Step 6: Update `DeserializationGenerator`

**File:** `src/FourSer.Gen/CodeGenerators/DeserializationGenerator.cs`

- **Objective:** Adapt the generator to use the new pre-calculated data.

- **Changes:**
    - Similar to the other generators, update the logic to use the new index and size properties from the data model instead of performing lookups and calculations at generation time.
