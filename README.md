# Serializer SourceGen

A high-performance .NET 9 source generator that automatically creates serialization and deserialization code for binary data structures using attributes and conventions.

## Overview

This project provides a compile-time source generator that creates efficient binary serialization methods for your classes and structs. It's designed for scenarios where you need fast, low-allocation binary serialization, such as network protocols, file formats, or inter-process communication.

## Features

- **Zero-allocation serialization** using `Span<byte>` and `ReadOnlySpan<byte>`
- **Compile-time code generation** for maximum performance
- **Support for primitive types** (byte, int, uint, long, string, etc.)
- **Collection serialization** with flexible count handling
- **Nested object support** with automatic dependency resolution
- **Custom count references** for complex data structures
- **Both classes and structs** supported

## Quick Start

### 1. Add the GenerateSerializer attribute

```csharp
using Serializer.Contracts;

[GenerateSerializer]
public partial class LoginPacket
{
    public byte Result;
    public uint UserID;
    public string Username;
}
```

### 2. Use the generated methods

```csharp
var packet = new LoginPacket
{
    Result = 1,
    UserID = 12345,
    Username = "player1"
};

// Get required buffer size
int size = LoginPacket.GetPacketSize(packet);

// Serialize
var buffer = new byte[size];
var span = new Span<byte>(buffer);
int bytesWritten = LoginPacket.Serialize(packet, span);

// Deserialize
var readSpan = new ReadOnlySpan<byte>(buffer);
var deserialized = LoginPacket.Deserialize(readSpan, out int bytesRead);
```

## Generated Interface

Each class marked with `[GenerateSerializer]` implements `ISerializable<T>`:

```csharp
public interface ISerializable<T> where T : ISerializable<T>
{
    static abstract int GetPacketSize(T obj);
    static abstract T Deserialize(ReadOnlySpan<byte> data, out int bytesRead);
    static abstract int Serialize(T obj, Span<byte> data);
}
```

## Collection Serialization

### Basic Collections

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    // Default: count stored as int32 (4 bytes)
    [SerializeCollection]
    public List<byte> Data { get; set; } = new();
}
```

### Custom Count Types

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    // Use ushort (2 bytes) for count
    [SerializeCollection(CountType = typeof(ushort))]
    public List<int> Numbers { get; set; } = new();

    // Use custom bit size for count
    [SerializeCollection(CountSize = 2)] // 2 bytes = 16 bits
    public List<string> Names { get; set; } = new();
}
```

### Count References

When the count is stored separately from the collection:

```csharp
[GenerateSerializer]
public partial class MyPacket
{
    public int EntityCount { get; set; }
    public string PacketName { get; set; }

    [SerializeCollection(CountSizeReference = nameof(EntityCount))]
    public List<Entity> Entities { get; set; } = new();
}
```

## Nested Objects

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

Support for polymorphic types using type discriminators. The generator supports two modes:

### 1. Explicit TypeId Property

When you have a TypeId property in your model:

```csharp
[GenerateSerializer]
public partial class PolymorphicEntity
{
    public int Id { get; set; }
    public int TypeId { get; set; } // Type discriminator
    
    [SerializePolymorphic(nameof(TypeId))]
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption(2, typeof(EntityType2))]
    public BaseEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseEntity
    {
    }
    
    [GenerateSerializer]
    public partial class EntityType1 : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class EntityType2 : BaseEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}
```

#### Usage

```csharp
var entity = new PolymorphicEntity
{
    Id = 100,
    TypeId = 1, // Indicates EntityType1
    Entity = new PolymorphicEntity.EntityType1 { Name = "Test" }
};

// Serialization automatically uses the correct type based on TypeId
var size = PolymorphicEntity.GetPacketSize(entity);
var buffer = new byte[size];
var bytesWritten = PolymorphicEntity.Serialize(entity, buffer);

// Deserialization automatically creates the correct type
var deserialized = PolymorphicEntity.Deserialize(buffer, out var bytesRead);
// deserialized.Entity will be of type EntityType1
```

#### Automatic TypeId Inference

The generator automatically ensures TypeId properties are synchronized with the actual object types during serialization:

```csharp
var entity = new PolymorphicEntity
{
    Id = 100,
    TypeId = 999, // Wrong TypeId!
    Entity = new PolymorphicEntity.EntityType1 { Name = "Test" }
};

// During serialization, TypeId is automatically corrected to 1
var bytesWritten = PolymorphicEntity.Serialize(entity, buffer);
// entity.TypeId is now 1 (corrected from 999)
```

This prevents serialization errors when the TypeId property doesn't match the actual object type.

### 2. Automatic TypeId Inference (No TypeId Property)

When you don't want a TypeId property in your model, the generator can automatically infer and handle the TypeId:

```csharp
[GenerateSerializer]
public partial class AutoPolymorphicEntity
{
    public int Id { get; set; }
    
    // No TypeId property - will be inferred automatically
    [SerializePolymorphic]
    [PolymorphicOption(1, typeof(AutoEntityType1))]
    [PolymorphicOption(2, typeof(AutoEntityType2))]
    public BaseAutoEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseAutoEntity
    {
    }
    
    [GenerateSerializer]
    public partial class AutoEntityType1 : BaseAutoEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class AutoEntityType2 : BaseAutoEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}
```

#### Usage

```csharp
var entity = new AutoPolymorphicEntity
{
    Id = 100,
    // No TypeId needed - inferred from actual object type
    Entity = new AutoPolymorphicEntity.AutoEntityType1 { Name = "Test" }
};

// TypeId is automatically inferred and written to the stream
var size = AutoPolymorphicEntity.GetPacketSize(entity);
var buffer = new byte[size];
var bytesWritten = AutoPolymorphicEntity.Serialize(entity, buffer);

// TypeId is read from stream and correct type is created
var deserialized = AutoPolymorphicEntity.Deserialize(buffer, out var bytesRead);
// deserialized.Entity will be of type AutoEntityType1
```

In this mode:
- During serialization: The actual object type is used to determine the TypeId, which is written to the stream
- During deserialization: The TypeId is read from the stream and used to create the correct object type
- The TypeId is not stored in your model, keeping it clean

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

- `List<T>` where T is any supported type
- Arrays (planned)

### Custom Types

- Any class or struct marked with `[GenerateSerializer]`
- Polymorphic types using `[SerializePolymorphic]` and `[PolymorphicOption]` attributes

## Project Structure

```
src/
├── Serializer.Contracts/          # Attributes and interfaces
│   ├── ISerializable.cs           # Main serialization interface
│   ├── GenerateSerializerAttribute.cs
│   └── SerializeCollectionAttribute.cs
├── Serializer.Generator/          # Source generator implementation
│   ├── SerializerGenerator.cs     # Main generator logic
│   └── ClassToGenerate.cs         # Data model for generation
└── Serializer.Consumer/           # Example usage and tests
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

## Requirements

- .NET 9.0 or later
- C# 12.0 or later (for static abstract interface members)

## Building

```bash
dotnet build
```

## Running Tests

```bash
dotnet run --project src/Serializer.Consumer
```

## Contributing

This project uses source generators to provide compile-time serialization code generation. When adding new features:

1. Update the generator logic in `SerializerGenerator.cs`
2. Add corresponding attributes in `Serializer.Contracts`
3. Create test cases in `Serializer.Consumer/UseCases`
4. Run the test suite to verify functionality

## License

[Add your license information here]
