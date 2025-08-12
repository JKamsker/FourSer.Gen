using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

public static class PolymorphicComparison
{
    public static void RunComparison()
    {
        Console.WriteLine("=== Polymorphic Serialization Comparison ===");
        
        // Test with explicit TypeId property
        var explicitEntity = new PolymorphicEntity
        {
            Id = 100,
            TypeId = 1,
            Entity = new PolymorphicEntity.EntityType1 { Name = "Explicit Test" }
        };
        
        var explicitSize = PolymorphicEntity.GetPacketSize(explicitEntity);
        var explicitBuffer = new byte[explicitSize];
        var explicitBytesWritten = PolymorphicEntity.Serialize(explicitEntity, explicitBuffer);
        
        Console.WriteLine($"Explicit TypeId approach:");
        Console.WriteLine($"  Model has TypeId property: {explicitEntity.TypeId}");
        Console.WriteLine($"  Packet size: {explicitSize} bytes");
        Console.WriteLine($"  Bytes written: {explicitBytesWritten}");
        
        // Test with automatic TypeId inference
        var autoEntity = new AutoPolymorphicEntity
        {
            Id = 100,
            Entity = new AutoPolymorphicEntity.AutoEntityType1 { Name = "Auto Test" }
        };
        
        var autoSize = AutoPolymorphicEntity.GetPacketSize(autoEntity);
        var autoBuffer = new byte[autoSize];
        var autoBytesWritten = AutoPolymorphicEntity.Serialize(autoEntity, autoBuffer);
        
        Console.WriteLine($"Automatic TypeId approach:");
        Console.WriteLine($"  Model has no TypeId property");
        Console.WriteLine($"  Packet size: {autoSize} bytes");
        Console.WriteLine($"  Bytes written: {autoBytesWritten}");
        
        // Both should have the same packet size since both write the TypeId to the stream
        Console.WriteLine($"Packet sizes match: {explicitSize == autoSize}");
        
        // Deserialize both
        var explicitDeserialized = PolymorphicEntity.Deserialize(explicitBuffer, out var explicitBytesRead);
        var autoDeserialized = AutoPolymorphicEntity.Deserialize(autoBuffer, out var autoBytesRead);
        
        Console.WriteLine($"Both deserialized successfully: {explicitDeserialized.Id == 100 && autoDeserialized.Id == 100}");
        Console.WriteLine($"Both have correct types: {explicitDeserialized.Entity is PolymorphicEntity.EntityType1 && autoDeserialized.Entity is AutoPolymorphicEntity.AutoEntityType1}");
        
        Console.WriteLine("=== Comparison Complete ===\n");
    }
}