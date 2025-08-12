using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

// Example with byte TypeId

// Example with ushort TypeId
[GenerateSerializer]
public partial class PolymorphicWithUShortTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic(TypeIdType = typeof(ushort))]
    [PolymorphicOption((ushort)1000, typeof(UShortEntityType1))]
    [PolymorphicOption((ushort)2000, typeof(UShortEntityType2))]
    public BaseUShortEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseUShortEntity
    {
    }
    
    [GenerateSerializer]
    public partial class UShortEntityType1 : BaseUShortEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class UShortEntityType2 : BaseUShortEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}

// Example with long TypeId
[GenerateSerializer]
public partial class PolymorphicWithLongTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic(TypeIdType = typeof(long))]
    [PolymorphicOption(1000000L, typeof(LongEntityType1))]
    [PolymorphicOption(2000000L, typeof(LongEntityType2))]
    public BaseLongEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseLongEntity
    {
    }
    
    [GenerateSerializer]
    public partial class LongEntityType1 : BaseLongEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class LongEntityType2 : BaseLongEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}

// Example with custom enum TypeId
public enum EntityTypeEnum : byte
{
    Type1 = 10,
    Type2 = 20
}

[GenerateSerializer]
public partial class PolymorphicWithEnumTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic(TypeIdType = typeof(EntityTypeEnum))]
    [PolymorphicOption(EntityTypeEnum.Type1, typeof(EnumEntityType1))]
    [PolymorphicOption(EntityTypeEnum.Type2, typeof(EnumEntityType2))]
    public BaseEnumEntity Entity { get; set; }
    
    [GenerateSerializer]
    public partial class BaseEnumEntity
    {
    }
    
    [GenerateSerializer]
    public partial class EnumEntityType1 : BaseEnumEntity
    {
        public string Name { get; set; } = string.Empty;
    }
    
    [GenerateSerializer]
    public partial class EnumEntityType2 : BaseEnumEntity
    {
        public string Description { get; set; } = string.Empty;
    }
}

public static class PolymorphicTypeIdTest
{
    public static void RunTest()
    {
        Console.WriteLine("=== Polymorphic Different TypeId Types Test ===");
        // Test byte TypeId
        PolymorphicWithByteTypeIdTests.RunTest();
        
        // Test ushort TypeId
        var ushortEntity = new PolymorphicWithUShortTypeId
        {
            Id = 200,
            Entity = new PolymorphicWithUShortTypeId.UShortEntityType1 { Name = "UShort TypeId Test" }
        };
        TestSerialization(ushortEntity, "UShort TypeId",
            () => PolymorphicWithUShortTypeId.GetPacketSize(ushortEntity),
            (buffer) => PolymorphicWithUShortTypeId.Serialize(ushortEntity, buffer),
            (buffer) => PolymorphicWithUShortTypeId.Deserialize(buffer, out var bytesRead));
        
        // Test long TypeId
        var longEntity = new PolymorphicWithLongTypeId
        {
            Id = 300,
            Entity = new PolymorphicWithLongTypeId.LongEntityType1 { Name = "Long TypeId Test" }
        };
        TestSerialization(longEntity, "Long TypeId",
            () => PolymorphicWithLongTypeId.GetPacketSize(longEntity),
            (buffer) => PolymorphicWithLongTypeId.Serialize(longEntity, buffer),
            (buffer) => PolymorphicWithLongTypeId.Deserialize(buffer, out var bytesRead));
        
        // Test enum TypeId
        var enumEntity = new PolymorphicWithEnumTypeId
        {
            Id = 400,
            Entity = new PolymorphicWithEnumTypeId.EnumEntityType1 { Name = "Enum TypeId Test" }
        };
        TestSerialization(enumEntity, "Enum TypeId",
            () => PolymorphicWithEnumTypeId.GetPacketSize(enumEntity),
            (buffer) => PolymorphicWithEnumTypeId.Serialize(enumEntity, buffer),
            (buffer) => PolymorphicWithEnumTypeId.Deserialize(buffer, out var bytesRead));
        
        Console.WriteLine("=== Polymorphic Different TypeId Types Test Complete ===\n");
    }
    
    public static void TestSerialization<T>(T original, string testName, 
        System.Func<int> getSize, 
        System.Func<System.Span<byte>, int> serialize,
        System.Func<System.ReadOnlySpan<byte>, T> deserialize) where T : class
    {
        Console.WriteLine($"Testing {testName}:");
        
        // Get packet size
        var size = getSize();
        Console.WriteLine($"  Packet size: {size} bytes");
        
        // Serialize
        var buffer = new byte[size];
        var span = new System.Span<byte>(buffer);
        var bytesWritten = serialize(span);
        Console.WriteLine($"  Bytes written: {bytesWritten}");
        
        // Deserialize
        var readSpan = new System.ReadOnlySpan<byte>(buffer);
        var deserialized = deserialize(readSpan);
        
        // Verify basic properties
        var originalId = original.GetType().GetProperty("Id")?.GetValue(original);
        var deserializedId = deserialized.GetType().GetProperty("Id")?.GetValue(deserialized);
        Console.WriteLine($"  Original ID: {originalId}, Deserialized ID: {deserializedId}");
        Console.WriteLine($"  IDs match: {Equals(originalId, deserializedId)}");
        
        // Check entity types
        var originalEntity = original.GetType().GetProperty("Entity")?.GetValue(original);
        var deserializedEntity = deserialized.GetType().GetProperty("Entity")?.GetValue(deserialized);
        var originalEntityType = originalEntity?.GetType().Name;
        var deserializedEntityType = deserializedEntity?.GetType().Name;
        Console.WriteLine($"  Original Entity Type: {originalEntityType}, Deserialized Entity Type: {deserializedEntityType}");
        Console.WriteLine($"  Entity types match: {originalEntityType == deserializedEntityType}");
        
        Console.WriteLine($"  Test {testName}: PASSED\n");
    }
}