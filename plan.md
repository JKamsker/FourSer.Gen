# Serializer Source Generator Plan

This document outlines the plan to create a .NET source generator for object serialization.

## Workflow Diagram

```mermaid
graph TD
    A[Compilation Starts] --> B{Find classes with [GenerateSerializer]};
    B --> C{For each class};
    C --> D[Analyze class members and attributes];
    D --> E{Generate partial class with ISerializable<T>};
    E --> F[Generate GetPacketSize method];
    E --> G[Generate Deserialize method];
    E --> H[Generate Serialize method];
    F --> I;
    G --> I;
    H --> I{Add generated source to compilation};
    I --> J[Compilation Finishes];
```

## Detailed Plan

- [ ] 1. **Project Setup**
  - [ ] 1.1. Create a .NET Class Library for the Source Generator.
  - [ ] 1.2. Add `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` NuGet packages.
  - [ ] 1.3. Create a second Class Library for shared contracts (attributes, interfaces).
  - [ ] 1.4. Create a Console Application for testing the generator.
- [ ] 2. **Define Core Contracts**
  - [ ] 2.1. In the contracts project, define the `ISerializable<T>` interface.
  - [ ] 2.2. In the contracts project, define the `GenerateSerializerAttribute` with its properties (`CountType`, `CountSize`, `CountSizeReference`).
- [ ] 3. **Implement the Incremental Generator**
  - [ ] 3.1. Create the main generator class implementing `IIncrementalGenerator`.
  - [ ] 3.2. Set up the pipeline in the `Initialize` method to find all partial classes with the `[GenerateSerializer]` attribute.
  - [ ] 3.3. Create a data model to hold information about the class and its members.
  - [ ] 3.4. Create a transform function to populate the data model from the syntax and semantic information.
  - [ ] 3.5. Register the source output step to trigger code generation.
- [ ] 4. **Code Generation Logic**
  - [ ] 4.1. Implement `GetPacketSize` method generation.
  - [ ] 4.2. Implement `Deserialize` method generation.
  - [ ] 4.3. Implement `Serialize` method generation.
- [ ] 5. **Handle Special Cases in Generation Logic**
  - [ ] 5.1. Handle `CountType` and `CountSize` for dynamic collections.
  - [ ] 5.2. Handle `CountSizeReference` for collections with an external count field.
  - [ ] 5.3. Handle nested serializable types.
- [ ] 6. **Testing**
  - [ ] 6.1. In the test project, reference the generator and contracts projects.
  - [ ] 6.2. Copy the extension files from `./Input/Extensions` to the test project.
  - [ ] 6.3. Create test classes covering all scenarios from `Task.md`.
  - [ ] 6.4. Write unit tests to serialize, deserialize, and verify data integrity.