using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class PolymorphicEntity
{
    public int Id { get; set; }
    public int TypeId { get; set; } // Used to identify the type during deserialization
    
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

public static class PolymorphicTest
{
    public static void RunTest()
    {
        Console.WriteLine("=== Polymorphic Serialization Test ===");
        
        // Test EntityType1 with correct TypeId
        var entity1 = new PolymorphicEntity
        {
            Id = 100,
            TypeId = 1,
            Entity = new PolymorphicEntity.EntityType1 { Name = "Test Entity 1" }
        };
        
        TestSerialization(entity1, "EntityType1 (Correct TypeId)");
        
        // Test EntityType1 with WRONG TypeId - should be auto-corrected
        var entity1Wrong = new PolymorphicEntity
        {
            Id = 101,
            TypeId = 999, // Wrong TypeId!
            Entity = new PolymorphicEntity.EntityType1 { Name = "Test Entity 1 Wrong TypeId" }
        };
        
        TestSerialization(entity1Wrong, "EntityType1 (Wrong TypeId - should auto-correct)");
        
        // Test EntityType2 with correct TypeId
        var entity2 = new PolymorphicEntity
        {
            Id = 200,
            TypeId = 2,
            Entity = new PolymorphicEntity.EntityType2 { Description = "Test Entity 2 Description" }
        };
        
        TestSerialization(entity2, "EntityType2 (Correct TypeId)");
        
        // Test EntityType2 with WRONG TypeId - should be auto-corrected
        var entity2Wrong = new PolymorphicEntity
        {
            Id = 201,
            TypeId = 1, // Wrong TypeId! Should be 2 for EntityType2
            Entity = new PolymorphicEntity.EntityType2 { Description = "Test Entity 2 Wrong TypeId" }
        };
        
        TestSerialization(entity2Wrong, "EntityType2 (Wrong TypeId - should auto-correct)");
        
        Console.WriteLine("=== Polymorphic Test Complete ===\n");
    }
    
    private static void TestSerialization(PolymorphicEntity original, string testName)
    {
        Console.WriteLine($"Testing {testName}:");
        
        var originalTypeId = original.TypeId;
        Console.WriteLine($"  Original TypeId before serialization: {originalTypeId}");
        
        // Get packet size
        var size = PolymorphicEntity.GetPacketSize(original);
        Console.WriteLine($"  Packet size: {size} bytes");
        
        // Serialize
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        var bytesWritten = PolymorphicEntity.Serialize(original, span);
        Console.WriteLine($"  Bytes written: {bytesWritten}");
        Console.WriteLine($"  TypeId after serialization: {original.TypeId} (auto-corrected: {original.TypeId != originalTypeId})");
        
        // Deserialize
        var readSpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = PolymorphicEntity.Deserialize(readSpan, out var bytesRead);
        Console.WriteLine($"  Bytes read: {bytesRead}");
        
        // Verify
        Console.WriteLine($"  Original ID: {original.Id}, Deserialized ID: {deserialized.Id}");
        Console.WriteLine($"  Serialized TypeId: {original.TypeId}, Deserialized TypeId: {deserialized.TypeId}");
        
        if (deserialized.TypeId == 1)
        {
            var origType1 = (PolymorphicEntity.EntityType1)original.Entity;
            var deserType1 = (PolymorphicEntity.EntityType1)deserialized.Entity;
            Console.WriteLine($"  Original Name: '{origType1.Name}', Deserialized Name: '{deserType1.Name}'");
            Console.WriteLine($"  Names match: {origType1.Name == deserType1.Name}");
        }
        else if (deserialized.TypeId == 2)
        {
            var origType2 = (PolymorphicEntity.EntityType2)original.Entity;
            var deserType2 = (PolymorphicEntity.EntityType2)deserialized.Entity;
            Console.WriteLine($"  Original Description: '{origType2.Description}', Deserialized Description: '{deserType2.Description}'");
            Console.WriteLine($"  Descriptions match: {origType2.Description == deserType2.Description}");
        }
        
        Console.WriteLine($"  Test {testName}: PASSED\n");
    }
}


[GenerateSerializer]
public partial class PolymorphicEntity1
{
    public int Id { get; set; }
    
    [SerializePolymorphic]
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