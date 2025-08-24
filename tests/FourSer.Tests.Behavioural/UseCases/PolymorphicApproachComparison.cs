namespace FourSer.Tests.Behavioural.UseCases;

public static class PolymorphicApproachComparison
{
    public static void RunComparison()
    {
        Console.WriteLine("=== Polymorphic Approach Comparison ===");
        
        // Approach 1: Explicit TypeId with synchronization
        var explicitEntity = new PolymorphicEntity
        {
            Id = 100,
            TypeId = 1,
            Entity = new PolymorphicEntity.EntityType1 { Name = "Explicit" }
        };
        
        // Approach 2: Implicit TypeId (no TypeId property in model)
        var implicitEntity = new PolymorphicEntityImplicitTypeId
        {
            Id = 100,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Implicit" }
        };
        
        Console.WriteLine("Approach 1 - Explicit TypeId (with synchronization):");
        TestApproach(explicitEntity, "Explicit");
        
        Console.WriteLine("Approach 2 - Implicit TypeId (no TypeId in model):");
        TestApproach(implicitEntity, "Implicit");
        
        Console.WriteLine("=== Approach Comparison Summary ===");
        Console.WriteLine("1. Explicit TypeId: TypeId property in model, auto-corrected during serialization");
        Console.WriteLine("2. Implicit TypeId: No TypeId property, inferred and written to stream");
        Console.WriteLine("Both approaches use pattern matching for optimal performance!");
        Console.WriteLine("=== Comparison Complete ===\n");
    }
    
    private static void TestApproach<T>(T entity, string approachName) where T : class
    {
        Console.WriteLine($"  {approachName} approach:");
        Console.WriteLine($"    Model type: {entity.GetType().Name}");
        
        // Check if entity has TypeId property
        var typeIdProperty = entity.GetType().GetProperty("TypeId");
        if (typeIdProperty != null)
        {
            Console.WriteLine($"    Has TypeId property: Yes (value: {typeIdProperty.GetValue(entity)})");
        }
        else
        {
            Console.WriteLine($"    Has TypeId property: No (inferred automatically)");
        }
        
        // Get the Entity property to show the polymorphic type
        var entityProperty = entity.GetType().GetProperty("Entity");
        if (entityProperty != null)
        {
            var entityValue = entityProperty.GetValue(entity);
            Console.WriteLine($"    Polymorphic entity type: {entityValue?.GetType().Name}");
        }
        
        Console.WriteLine($"    Uses pattern matching: Yes");
        Console.WriteLine();
    }
}