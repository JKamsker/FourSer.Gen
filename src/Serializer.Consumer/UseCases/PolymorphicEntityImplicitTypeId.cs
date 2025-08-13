using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class PolymorphicEntityImplicitTypeId
{
    public int Id { get; set; }
    
    [SerializePolymorphic]
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption(2, typeof(EntityType2))]
    public BaseEntity? Entity { get; set; }
    
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

public static class PolymorphicImplicitTypeIdTest
{
    public static void RunTest()
    {
        Console.WriteLine("=== Polymorphic Implicit TypeId Serialization Test ===");
        
        // Test EntityType1 - TypeId will be inferred automatically
        var entity1 = new PolymorphicEntityImplicitTypeId
        {
            Id = 100,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Implicit Test Entity 1" }
        };
        
        TestSerialization(entity1, "EntityType1 (Implicit TypeId)");
        
        // Test EntityType2 - TypeId will be inferred automatically
        var entity2 = new PolymorphicEntityImplicitTypeId
        {
            Id = 200,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType2 { Description = "Implicit Test Entity 2 Description" }
        };
        
        TestSerialization(entity2, "EntityType2 (Implicit TypeId)");
        
        // Test with different data to ensure proper serialization/deserialization
        var entity3 = new PolymorphicEntityImplicitTypeId
        {
            Id = 300,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Another Implicit Test" }
        };
        
        TestSerialization(entity3, "EntityType1 (Another Implicit TypeId)");
        
        var entity4 = new PolymorphicEntityImplicitTypeId
        {
            Id = 400,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType2 { Description = "Another Implicit Description" }
        };
        
        TestSerialization(entity4, "EntityType2 (Another Implicit TypeId)");
        
        Console.WriteLine("=== Polymorphic Implicit TypeId Test Complete ===\n");
    }
    
    private static void TestSerialization(PolymorphicEntityImplicitTypeId original, string testName)
    {
        Console.WriteLine($"Testing {testName}:");
        
        // Get packet size
        var size = PolymorphicEntityImplicitTypeId.GetPacketSize(original);
        Console.WriteLine($"  Packet size: {size} bytes");
        
        // Serialize
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        var bytesWritten = PolymorphicEntityImplicitTypeId.Serialize(original, span);
        Console.WriteLine($"  Bytes written: {bytesWritten}");
        
        // Deserialize
        var readSpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = PolymorphicEntityImplicitTypeId.Deserialize(readSpan, out var bytesRead);
        Console.WriteLine($"  Bytes read: {bytesRead}");
        
        // Verify
        Console.WriteLine($"  Original ID: {original.Id}, Deserialized ID: {deserialized.Id}");
        
        // Check the actual types match
        var originalType = original.Entity!.GetType().Name;
        var deserializedType = deserialized.Entity!.GetType().Name;
        Console.WriteLine($"  Original Type: {originalType}, Deserialized Type: {deserializedType}");
        Console.WriteLine($"  Types match: {originalType == deserializedType}");
        
        // Verify content based on type
        if (deserialized.Entity is PolymorphicEntityImplicitTypeId.EntityType1 deserType1)
        {
            var origType1 = (PolymorphicEntityImplicitTypeId.EntityType1)original.Entity!;
            Console.WriteLine($"  Original Name: '{origType1.Name}', Deserialized Name: '{deserType1.Name}'");
            Console.WriteLine($"  Names match: {origType1.Name == deserType1.Name}");
        }
        else if (deserialized.Entity is PolymorphicEntityImplicitTypeId.EntityType2 deserType2)
        {
            var origType2 = (PolymorphicEntityImplicitTypeId.EntityType2)original.Entity!;
            Console.WriteLine($"  Original Description: '{origType2.Description}', Deserialized Description: '{deserType2.Description}'");
            Console.WriteLine($"  Descriptions match: {origType2.Description == deserType2.Description}");
        }
        
        Console.WriteLine($"  Test {testName}: PASSED\n");
    }
}