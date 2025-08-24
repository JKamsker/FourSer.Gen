using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

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

public class PolymorphicEntityImplicitTypeIdTests
{
    [Fact]
    public void TestEntityType1()
    {
        var entity = new PolymorphicEntityImplicitTypeId
        {
            Id = 100,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType1 { Name = "Implicit Test Entity 1" }
        };
        TestSerialization(entity);
    }

    [Fact]
    public void TestEntityType2()
    {
        var entity = new PolymorphicEntityImplicitTypeId
        {
            Id = 200,
            Entity = new PolymorphicEntityImplicitTypeId.EntityType2 { Description = "Implicit Test Entity 2 Description" }
        };
        TestSerialization(entity);
    }

    private static void TestSerialization(PolymorphicEntityImplicitTypeId original)
    {
        var size = PolymorphicEntityImplicitTypeId.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        PolymorphicEntityImplicitTypeId.Serialize(original, span);

        var readSpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = PolymorphicEntityImplicitTypeId.Deserialize(readSpan);

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Entity!.GetType(), deserialized.Entity!.GetType());

        if (original.Entity is PolymorphicEntityImplicitTypeId.EntityType1 originalType1)
        {
            var deserializedType1 = Assert.IsType<PolymorphicEntityImplicitTypeId.EntityType1>(deserialized.Entity);
            Assert.Equal(originalType1.Name, deserializedType1.Name);
        }
        else if (original.Entity is PolymorphicEntityImplicitTypeId.EntityType2 originalType2)
        {
            var deserializedType2 = Assert.IsType<PolymorphicEntityImplicitTypeId.EntityType2>(deserialized.Entity);
            Assert.Equal(originalType2.Description, deserializedType2.Description);
        }
    }
}