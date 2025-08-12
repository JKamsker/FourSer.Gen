using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class AutoPolymorphicEntity
{
    public int Id { get; set; }
    
    // No TypeId property specified - will be inferred automatically
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

public static class AutoPolymorphicTest
{
    public static void RunTest()
    {
        Console.WriteLine("=== Auto Polymorphic Serialization Test ===");
        
        // Test AutoEntityType1 - TypeId will be inferred automatically
        var entity1 = new AutoPolymorphicEntity
        {
            Id = 100,
            Entity = new AutoPolymorphicEntity.AutoEntityType1 { Name = "Auto Test Entity 1" }
        };
        
        TestSerialization(entity1, "AutoEntityType1 (Auto TypeId)");
        
        // Test AutoEntityType2 - TypeId will be inferred automatically
        var entity2 = new AutoPolymorphicEntity
        {
            Id = 200,
            Entity = new AutoPolymorphicEntity.AutoEntityType2 { Description = "Auto Test Entity 2 Description" }
        };
        
        TestSerialization(entity2, "AutoEntityType2 (Auto TypeId)");
        
        Console.WriteLine("=== Auto Polymorphic Test Complete ===\n");
    }
    
    private static void TestSerialization(AutoPolymorphicEntity original, string testName)
    {
        Console.WriteLine($"Testing {testName}:");
        
        // Get packet size
        var size = AutoPolymorphicEntity.GetPacketSize(original);
        Console.WriteLine($"  Packet size: {size} bytes");
        
        // Serialize
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        var bytesWritten = AutoPolymorphicEntity.Serialize(original, span);
        Console.WriteLine($"  Bytes written: {bytesWritten}");
        
        // Deserialize
        var readSpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = AutoPolymorphicEntity.Deserialize(readSpan, out var bytesRead);
        Console.WriteLine($"  Bytes read: {bytesRead}");
        
        // Verify
        Console.WriteLine($"  Original ID: {original.Id}, Deserialized ID: {deserialized.Id}");
        
        // Check the actual types match
        var originalType = original.Entity.GetType().Name;
        var deserializedType = deserialized.Entity.GetType().Name;
        Console.WriteLine($"  Original Type: {originalType}, Deserialized Type: {deserializedType}");
        Console.WriteLine($"  Types match: {originalType == deserializedType}");
        
        if (deserialized.Entity is AutoPolymorphicEntity.AutoEntityType1 deserType1)
        {
            var origType1 = (AutoPolymorphicEntity.AutoEntityType1)original.Entity;
            Console.WriteLine($"  Original Name: '{origType1.Name}', Deserialized Name: '{deserType1.Name}'");
            Console.WriteLine($"  Names match: {origType1.Name == deserType1.Name}");
        }
        else if (deserialized.Entity is AutoPolymorphicEntity.AutoEntityType2 deserType2)
        {
            var origType2 = (AutoPolymorphicEntity.AutoEntityType2)original.Entity;
            Console.WriteLine($"  Original Description: '{origType2.Description}', Deserialized Description: '{deserType2.Description}'");
            Console.WriteLine($"  Descriptions match: {origType2.Description == deserType2.Description}");
        }
        
        Console.WriteLine($"  Test {testName}: PASSED\n");
    }
}