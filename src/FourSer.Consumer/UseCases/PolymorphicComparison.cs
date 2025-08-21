namespace FourSer.Consumer.UseCases;

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
        
        // Test with implicit TypeId inference
        var implicitEntity = new PolymorphicEntityImplicitTypeId
        {
            Id = 100,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Implicit Test" }
        };
        
        var implicitSize = PolymorphicEntityImplicitTypeId.GetPacketSize(implicitEntity);
        var implicitBuffer = new byte[implicitSize];
        var implicitBytesWritten = PolymorphicEntityImplicitTypeId.Serialize(implicitEntity, implicitBuffer);
        
        Console.WriteLine($"Implicit TypeId approach:");
        Console.WriteLine($"  Model has no TypeId property");
        Console.WriteLine($"  Packet size: {implicitSize} bytes");
        Console.WriteLine($"  Bytes written: {implicitBytesWritten}");
        
        // Compare packet sizes
        Console.WriteLine($"Packet sizes match: {explicitSize == implicitSize}");
        
        // Deserialize both
        var explicitDeserialized = PolymorphicEntity.Deserialize(explicitBuffer);
        var implicitDeserialized = PolymorphicEntityImplicitTypeId.Deserialize(implicitBuffer);
        
        Console.WriteLine($"Both deserialized successfully: {explicitDeserialized.Id == 100 && implicitDeserialized.Id == 100}");
        Console.WriteLine($"Both have correct types: {explicitDeserialized.Entity is PolymorphicEntity.EntityType1 && implicitDeserialized.Entity is PolymorphicEntityImplicitTypeId.EntityType1}");
        
        Console.WriteLine("=== Comparison Complete ===\n");
    }
}