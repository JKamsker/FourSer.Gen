using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class PolymorphicEntity
{
    public int Id { get; set; }
    public int TypeId { get; set; } // Used to identify the type during deserialization

    [SerializePolymorphic(nameof(TypeId))]
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

public class PolymorphicEntityTests
{
    [Fact]
    public void TestEntityType1WithCorrectTypeId()
    {
        var entity = new PolymorphicEntity
        {
            Id = 100,
            TypeId = 1,
            Entity = new PolymorphicEntity.EntityType1 { Name = "Test Entity 1" }
        };
        TestSerialization(entity);
    }

    [Fact]
    public void TestEntityType1WithWrongTypeId()
    {
        var entity = new PolymorphicEntity
        {
            Id = 101,
            TypeId = 999, // Wrong TypeId!
            Entity = new PolymorphicEntity.EntityType1 { Name = "Test Entity 1 Wrong TypeId" }
        };
        TestSerialization(entity);
    }

    [Fact]
    public void TestEntityType2WithCorrectTypeId()
    {
        var entity = new PolymorphicEntity
        {
            Id = 200,
            TypeId = 2,
            Entity = new PolymorphicEntity.EntityType2 { Description = "Test Entity 2 Description" }
        };
        TestSerialization(entity);
    }

    [Fact]
    public void TestEntityType2WithWrongTypeId()
    {
        var entity = new PolymorphicEntity
        {
            Id = 201,
            TypeId = 1, // Wrong TypeId! Should be 2 for EntityType2
            Entity = new PolymorphicEntity.EntityType2 { Description = "Test Entity 2 Wrong TypeId" }
        };
        TestSerialization(entity);
    }

    private static void TestSerialization(PolymorphicEntity original)
    {
        var size = PolymorphicEntity.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        PolymorphicEntity.Serialize(original, span);

        var readSpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = PolymorphicEntity.Deserialize(readSpan);

        Assert.Equal(original.Id, deserialized.Id);

        if (original.Entity is PolymorphicEntity.EntityType1 originalType1)
        {
            var deserializedType1 = Assert.IsType<PolymorphicEntity.EntityType1>(deserialized.Entity);
            Assert.Equal(originalType1.Name, deserializedType1.Name);
            Assert.Equal(1, deserialized.TypeId);
        }
        else if (original.Entity is PolymorphicEntity.EntityType2 originalType2)
        {
            var deserializedType2 = Assert.IsType<PolymorphicEntity.EntityType2>(deserialized.Entity);
            Assert.Equal(originalType2.Description, deserializedType2.Description);
            Assert.Equal(2, deserialized.TypeId);
        }
    }
}
