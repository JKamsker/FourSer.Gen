using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

public class PolymorphicComparisonTests
{
    [Fact]
    public void CompareExplicitAndImplicitApproaches()
    {
        // Test with explicit TypeId property
        var explicitEntity = new PolymorphicEntity
        {
            Id = 100,
            TypeId = 1,
            Entity = new PolymorphicEntity.EntityType1 { Name = "Explicit Test" }
        };

        var explicitSize = PolymorphicEntity.GetPacketSize(explicitEntity);
        var explicitBuffer = new byte[explicitSize];
        PolymorphicEntity.Serialize(explicitEntity, explicitBuffer);

        // Test with implicit TypeId inference
        var implicitEntity = new PolymorphicEntityImplicitTypeId
        {
            Id = 100,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Implicit Test" }
        };

        var implicitSize = PolymorphicEntityImplicitTypeId.GetPacketSize(implicitEntity);
        var implicitBuffer = new byte[implicitSize];
        PolymorphicEntityImplicitTypeId.Serialize(implicitEntity, implicitBuffer);

        Assert.Equal(explicitSize, implicitSize);

        var explicitDeserialized = PolymorphicEntity.Deserialize(explicitBuffer);
        var implicitDeserialized = PolymorphicEntityImplicitTypeId.Deserialize(implicitBuffer);

        Assert.Equal(100, explicitDeserialized.Id);
        Assert.Equal(100, implicitDeserialized.Id);
        Assert.IsType<PolymorphicEntity.EntityType1>(explicitDeserialized.Entity);
        Assert.IsType<PolymorphicEntityImplicitTypeId.EntityType1>(implicitDeserialized.Entity);
    }
}