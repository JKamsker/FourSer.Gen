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