# ![FourSer.Gen](resources/logo/logo-long.png)
[![Build Status](https://github.com/JKamsker/GeminiSerializer.SG/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/GeminiSerializer.SG/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/FourSer.Gen.svg)](https://www.nuget.org/packages/FourSer.Gen)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FourSer.Gen.svg)](https://www.nuget.org/packages/FourSer.Gen)
[![GitHub issues](https://img.shields.io/github/issues/JKamsker/GeminiSerializer.SG)](https://github.com/JKamsker/GeminiSerializer.SG/issues)
[![GitHub pull requests](https://img.shields.io/github/issues-pr/JKamsker/GeminiSerializer.SG)](https://github.com/JKamsker/GeminiSerializer.SG/pulls)

A high-performance .NET 9 source generator that automatically creates serialization and deserialization code for binary data structures using attributes and conventions.

## Overview

This project provides a compile-time source generator that creates efficient binary serialization methods for your classes and structs. It's designed for scenarios where you need fast, low-allocation binary serialization, such as network protocols, file formats, or inter-process communication.

## Features

- **High-Performance Serialization**: Zero-allocation serialization and deserialization using `Span<byte>` and `ReadOnlySpan<byte>`.
- **Compile-Time Code Generation**: Eliminates runtime reflection, ensuring maximum performance.
- **Wide Type Support**: Supports all primitive types, strings, and a comprehensive range of collection types.
- **Nested Objects**: Automatically handles serialization of complex object graphs with nested classes and structs.
- **Flexible Collection Handling**:
    - **Custom Count Prefixes**: Specify the integer type used for collection counts (e.g., `byte`, `ushort`, `int`).
    - **Fixed-Size Collections**: Serialize collections with a constant number of elements without a count prefix.
    - **Dynamic Count References**: Link a collection's count to another property in the class.
    - **Unlimited Collections**: Serialize collections that consume the rest of the data stream.
- **Advanced Polymorphic Serialization**:
    - **Automatic Type Inference**: The generator can automatically handle type discriminators without needing a `TypeId` property in your model.
    - **Explicit Type Discriminators**: Link polymorphism to a property in your model.
    - **Custom Discriminator Types**: Use `byte`, `ushort`, `long`, or enums for type discriminators to save space.
    - **Polymorphic Collections**: Serialize collections of different types, either with a single type discriminator for the whole collection or individual discriminators for each element.
- **Easy to Use**: Simply add attributes to your data structures to enable serialization.

## Quick Start

### 1. Install the NuGet Package

First, add the `FourSer.Gen` package to your project:

```bash
dotnet add package FourSer.Gen
```

### 2. Define Your Data Structures

Create a `partial` class or struct and add the `[GenerateSerializer]` attribute. All properties and fields will be automatically included in the serialization.

```csharp
// In your project, e.g., in a file named "Packets.cs"
using FourSer.Contracts;

[GenerateSerializer]
public partial class Player
{
    public uint Id { get; set; }
    public string Username { get; set; }
    public float Health { get; set; }
}

[GenerateSerializer]
public partial class GameState
{
    public long GameId { get; set; }

    [SerializeCollection] // This attribute is needed for collections
    public List<Player> Players { get; set; }
}
```

### 3. Use the Generated Methods

The source generator creates static `GetPacketSize`, `Serialize`, and `Deserialize` methods on your types.

```csharp
// Create an instance of your data structure
var state = new GameState
{
    GameId = 98765,
    Players = new List<Player>
    {
        new() { Id = 1, Username = "Hero", Health = 100.0f },
        new() { Id = 2, Username = "Villain", Health = 85.5f }
    }
};

// 1. Get the required buffer size
int size = GameState.GetPacketSize(state);

// 2. Serialize the object into a buffer
var buffer = new byte[size];
var span = new Span<byte>(buffer);
int bytesWritten = GameState.Serialize(state, span);

// The buffer now contains the binary representation of your object
// You can now send it over the network, save it to a file, etc.

// 3. Deserialize the object from the buffer
var readSpan = new ReadOnlySpan<byte>(buffer);
var deserializedState = GameState.Deserialize(readSpan);

// Now you have a deep copy of the original object
Console.WriteLine($"Game ID: {deserializedState.GameId}");
foreach (var player in deserializedState.Players)
{
    Console.WriteLine($"- Player: {player.Username}, Health: {player.Health}");
}
```

## Generated Interface

Each class marked with `[GenerateSerializer]` implements `ISerializable<T>`. This interface provides the core methods for serialization and deserialization.

```csharp
public interface ISerializable<T> where T : ISerializable<T>
{
    // Calculates the total size in bytes required to serialize the object.
    static abstract int GetPacketSize(T obj);

    // Serializes the object into the provided span.
    static abstract int Serialize(T obj, Span<byte> data);

    // Serializes the object into the provided stream.
    static abstract void Serialize(T obj, Stream stream);

    // Deserializes an object from the provided span.
    // The span is advanced by the number of bytes read.
    static abstract T Deserialize(ref ReadOnlySpan<byte> data);

    // Deserializes an object from the provided span without advancing it.
    static abstract T Deserialize(ReadOnlySpan<byte> data);

    // Deserializes an object from the provided stream.
    static abstract T Deserialize(Stream stream);
}
```

## Collection Serialization

The generator provides powerful options for serializing collections using the `[SerializeCollection]` attribute.

### Controlling the Count Prefix

By default, the generator prefixes a collection with an `int` (4 bytes) to store the number of elements. You can customize this behavior.

#### 1. Custom Count Type

Use the `CountType` property to specify a different integer type for the count prefix. This is useful for optimizing space.

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    // Use ushort (2 bytes) for the count prefix instead of the default int (4 bytes).
    [SerializeCollection(CountType = typeof(ushort))]
    public List<int> Numbers { get; set; } = new();
}
```

#### 2. Fixed-Size Collections

If the collection always has a fixed number of elements, use `CountSize`. This completely removes the count prefix from the binary data, saving space and improving performance.

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    // Always serialize exactly 16 bytes. No count is written to the stream.
    // If the collection has fewer than 16 items, an exception will be thrown.
    [SerializeCollection(CountSize = 16)]
    public byte[] Data { get; set; } = new byte[16];
}
```

#### 3. Dynamic Count Reference

If the collection's count is stored in another property, use `CountSizeReference` to link to it. This is common in protocols where a field specifies the length of a subsequent list.

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    public byte NameLength { get; set; }

    [SerializeCollection(CountSizeReference = nameof(NameLength))]
    public List<char> Name { get; set; }
}
```

#### 4. Unlimited Collections

For collections that should be serialized until the end of the data stream, use the `Unlimited` property. This is useful for top-level objects or when the length is implicitly known.

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    public int SomeHeader { get; set; }

    [SerializeCollection(Unlimited = true)]
    public List<byte> Payload { get; set; }
}
```

### Polymorphic Collections

The generator supports serializing collections of polymorphic types. This is useful when a list can contain objects of different derived types.

#### 1. Homogeneous Polymorphic Collections (`SingleTypeId`)

If all elements in the collection are of the same derived type, you can use `PolymorphicMode.SingleTypeId`. A single type discriminator is written once for the entire collection.

```csharp
[GenerateSerializer]
public partial class Scene
{
    public byte EntityType { get; set; } // Determines the type for all entities

    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = nameof(EntityType))]
    [PolymorphicOption((byte)1, typeof(Player))]
    [PolymorphicOption((byte)2, typeof(Monster))]
    public List<Entity> Entities { get; set; }
}
```

#### 2. Heterogeneous Polymorphic Collections (`IndividualTypeIds`)

If the elements in the collection can be of different derived types, use `PolymorphicMode.IndividualTypeIds`. Each element is prefixed with its own type discriminator.

```csharp
[GenerateSerializer]
public partial class Inventory
{
    // Each item in the list will have its own type ID (byte) written before it.
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public List<Item> Items { get; set; }
}
```

## Nested Objects
The generator automatically handles nested objects, as long as the nested types are also marked with `[GenerateSerializer]`.

```csharp
[GenerateSerializer]
public partial class ContainerPacket
{
    public int Id;
    public NestedData Data;
}

[GenerateSerializer]
public partial class NestedData
{
    public string Name;
    public float Value;
}
```

## Polymorphic Serialization

The generator supports serializing fields and properties that can hold one of several different types, which is known as polymorphic serialization. This is configured using the `[SerializePolymorphic]` and `[PolymorphicOption]` attributes.

There are two main approaches to handle the type discriminator (the value that identifies which concrete type is being used).

### Approach 1: Implicit Type Discriminator

In this approach, the type discriminator is written to and read from the binary stream, but it is not stored as a property in your model. This keeps your data models clean.

```csharp
[GenerateSerializer]
public partial class AutoPolymorphicEntity
{
    public int Id { get; set; }
    
    // The type discriminator will be inferred automatically.
    [SerializePolymorphic]
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption(2, typeof(EntityType2))]
    public BaseEntity Entity { get; set; }
}
```

- **Serialization**: The generator checks the actual type of `Entity` and writes the corresponding ID (`1` or `2`) to the stream.
- **Deserialization**: The generator reads the ID from the stream and creates an instance of the correct type (`EntityType1` or `EntityType2`).

### Approach 2: Explicit Type Discriminator

In this approach, the type discriminator is linked to a property in your model. The generator will use this property to determine which type to serialize or deserialize.

```csharp
[GenerateSerializer]
public partial class PolymorphicEntity
{
    public int Id { get; set; }
    public int TypeId { get; set; } // The type discriminator property

    [SerializePolymorphic(nameof(TypeId))] // Link to the TypeId property
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption(2, typeof(EntityType2))]
    public BaseEntity Entity { get; set; }
}
```

A key feature of this approach is that the generator automatically synchronizes the `TypeId` property during serialization. If you assign an `EntityType1` to the `Entity` property, the `TypeId` will be automatically set to `1` before serialization, preventing inconsistencies.

```csharp
var entity = new PolymorphicEntity
{
    Id = 100,
    TypeId = 999, // This value will be ignored and corrected
    Entity = new EntityType1 { Name = "Test" }
};

// During serialization, the generator will set entity.TypeId to 1.
var bytesWritten = PolymorphicEntity.Serialize(entity, buffer);
```

### Customizing the Type Discriminator Type

To save space, you can change the underlying type of the type discriminator from the default `int` to a smaller type like `byte` or `ushort`, or even an `enum`. This is done using the `TypeIdType` property on the `[SerializePolymorphic]` attribute.

```csharp
// Using byte (1 byte)
[SerializePolymorphic(TypeIdType = typeof(byte))]
[PolymorphicOption((byte)1, typeof(EntityType1))]
[PolymorphicOption((byte)2, typeof(EntityType2))]
public BaseEntity Entity { get; set; }

// Using a custom enum (backed by ushort)
public enum EntityType : ushort
{
    Type1 = 100,
    Type2 = 200
}

[SerializePolymorphic(TypeIdType = typeof(EntityType))]
[PolymorphicOption(EntityType.Type1, typeof(EntityType1))]
[PolymorphicOption(EntityType.Type2, typeof(EntityType2))]
public BaseEntity Entity { get; set; }
```

Using custom discriminator types offers several benefits:
- **Space Efficiency**: A `byte` uses 1 byte, a `ushort` uses 2, and an `int` uses 4. Choose the smallest type that fits your needs.
- **Type Safety**: Enums provide strong typing and make your code more readable and maintainable.
- **Automatic Casting**: The generator handles all necessary type conversions automatically.

## Custom Serializers

For special cases where the default serialization logic is not sufficient, you can provide your own custom serializer for any given type. This is useful for handling legacy binary formats, complex data structures, or types that require special encoding.

### 1. Create a Custom Serializer

A custom serializer is a class that implements the `ISerializer<T>` interface, where `T` is the type you want to serialize.

```csharp
public interface ISerializer<T>
{
    int GetPacketSize(T obj);
    int Serialize(T obj, Span<byte> data);
    void Serialize(T obj, Stream stream);
    T Deserialize(ref ReadOnlySpan<byte> data);
    T Deserialize(Stream stream);
}
```

Here is an example of a custom serializer for handling MFC-style Unicode strings, which have a specific length prefix format:

```csharp
public class MfcStringSerializer : ISerializer<string>
{
    public int GetPacketSize(string obj) { /* ... */ }
    public int Serialize(string obj, Span<byte> data) { /* ... */ }
    public void Serialize(string obj, Stream stream) { /* ... */ }
    public string Deserialize(ref ReadOnlySpan<byte> data) { /* ... */ }
    public string Deserialize(Stream stream) { /* ... */ }
}
```

### 2. Apply the Custom Serializer

You can apply a custom serializer in two ways:

#### On a Specific Property

Use the `[Serializer(typeof(MySerializer))]` attribute on a property to override its serialization logic.

```csharp
[GenerateSerializer]
public partial class LegacyPacket
{
    public int PlayerId { get; set; }

    [Serializer(typeof(MfcStringSerializer))]
    public string PlayerName { get; set; }
}
```

In this example, `PlayerName` will be serialized using `MfcStringSerializer`, while `PlayerId` will use the default integer serialization.

#### As a Default for a Type

Use the `[DefaultSerializer(typeof(TargetType), typeof(MySerializer))]` attribute on a class to set a default serializer for all properties of a specific type within that class.

```csharp
[GenerateSerializer]
[DefaultSerializer(typeof(string), typeof(MfcStringSerializer))]
public partial class AllMfcStringsPacket
{
    // This will use MfcStringSerializer by default
    public string PlayerName { get; set; }

    // This will also use MfcStringSerializer
    public string GuildName { get; set; }

    // You can still override the default if needed
    [Serializer(typeof(StandardStringSerializer))] // Assuming a standard one exists
    public string ChatMessage { get; set; }
}
```
This approach is useful when an entire class or data structure consistently uses a non-standard format for a certain type.

## Supported Types

### Primitive Types
- `byte`, `sbyte`
- `short`, `ushort`
- `int`, `uint`
- `long`, `ulong`
- `float`, `double`
- `bool`
- `string` (UTF-8 encoded with length prefix)

### Collections
The generator supports a wide range of collection types, where `T` can be any supported primitive, custom struct/class, or polymorphic type.

- `List<T>`
- `T[]` (Arrays)
- `ICollection<T>`
- `IEnumerable<T>`
- `IList<T>`
- `IReadOnlyCollection<T>`
- `IReadOnlyList<T>`
- `System.Collections.ObjectModel.Collection<T>`
- `System.Collections.ObjectModel.ObservableCollection<T>`
- `System.Collections.Concurrent.ConcurrentBag<T>`
- `HashSet<T>`
- `Queue<T>`
- `Stack<T>`
- `LinkedList<T>`
- `SortedSet<T>`
- `ImmutableList<T>`
- `ImmutableArray<T>`
- `ImmutableHashSet<T>`
- `ImmutableQueue<T>`
- `ImmutableStack<T>`
- `ImmutableSortedSet<T>`

### Custom Types
- Any `partial` class or struct marked with `[GenerateSerializer]`.
- Polymorphic types configured with `[SerializePolymorphic]` and `[PolymorphicOption]` attributes.
- `enum` types are serialized based on their underlying integer type.

## Project Structure

```
src/
├── FourSer.Contracts/          # Attributes and interfaces
│   ├── ISerializable.cs           # Main serialization interface
│   ├── GenerateSerializerAttribute.cs
│   └── SerializeCollectionAttribute.cs
├── FourSer.Gen/          # Source generator implementation
│   ├── SerializerGenerator.cs     # Main generator logic
│   └── ClassToGenerate.cs         # Data model for generation
└── FourSer.Consumer/           # Example usage and tests
    ├── UseCases/                  # Example packet definitions
    ├── Extensions/                # Span read/write extensions
    └── Program.cs                 # Test runner
```

## Performance Characteristics

- **Zero allocations** during serialization/deserialization
- **Compile-time code generation** eliminates reflection overhead
- **Direct memory access** using Span<T> for maximum throughput
- **Pattern matching** for polymorphic type detection (faster than reflection)
- **Little-endian byte order** for cross-platform compatibility
- **UTF-8 string encoding** with length prefixes

## Example: Game Network Protocol

```csharp
[GenerateSerializer]
public partial class LoginAckPacket
{
    public byte bResult;
    public uint dwUserID;
    public uint dwKickID;
    public uint dwKEY;
    public uint Address;
    public ushort Port;
    public byte bCreateCardCnt;
    public byte bInPcRoom;
    public uint dwPremiumPcRoom;
    public long dCurrentTime;
    public long dKey;
}

// Usage
var loginAck = new LoginAckPacket
{
    bResult = 1,
    dwUserID = 12345,
    // ... set other fields
};

var size = LoginAckPacket.GetPacketSize(loginAck);
var buffer = new byte[size];
var bytesWritten = LoginAckPacket.Serialize(loginAck, buffer);

// Send buffer over network...

// On receive:
var received = LoginAckPacket.Deserialize(receivedBuffer, out var bytesRead);
```

## Misc
### String Behavior
- Strings are serialized as UTF-8 with a length prefix ()


## Requirements

- .NET 9.0 or later
- C# 12.0 or later (for static abstract interface members)

## Building

```bash
dotnet build
```

## Testing

The solution includes a comprehensive suite of tests to ensure correctness and stability.

-   **`FourSer.Tests`**: Contains snapshot tests for the source generator using `Verify.Xunit`. These tests take input source code, run the generator, and compare the output against approved snapshots. This ensures that any change to the generated code is intentional.

    When a snapshot test fails, `Verify` will create a `.received.txt` file next to the `.verified.txt` file. To approve the changes, you can use a diff tool to compare the two files and then copy the content of the received file to the verified file. Many IDEs and diff tools provide a way to do this with a single click.

    Alternatively, you can use the following bash commands:

    To accept all changes:
    ```bash
    find . -name "*.received.txt" | while read received_file; do
      verified_file="${received_file%.received.txt}.verified.txt"
      mv "$received_file" "$verified_file"
    done
    ```

    To accept a specific change:
    ```bash
    mv path/to/your.received.txt path/to/your.verified.txt
    ```

-   **`FourSer.Analyzers.Test`**: Contains unit tests for the Roslyn analyzers. These tests ensure that the analyzers correctly identify issues in the source code and that the code fixes work as expected.

-   **`FourSer.Tests.Behavioural`**: Contains behavioural tests that use the generated serializers to perform round-trip serialization and deserialization of various data structures. These tests verify the runtime behavior of the generated code.

-   **`Serializer.Package.Tests`**: An integration test project that consumes the `FourSer.Gen` NuGet package. This test ensures that the package works correctly in a real-world scenario, from installation to usage.

To run all tests, use the following command from the root of the repository:

```bash
dotnet test
```

## Contributing

This project uses source generators to provide compile-time serialization code generation. When adding new features:

1. Update the generator logic in `SerializerGenerator.cs`
2. Add corresponding attributes in `FourSer.Contracts`
3. Create test cases in `FourSer.Consumer/UseCases`
4. Run the test suite to verify functionality

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
