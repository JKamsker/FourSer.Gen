using FourSer.Contracts;
using System.Collections.Generic;

namespace FourSer.Tests.Behavioural.Polymorphism;

public abstract partial class Animal
{
    public string? Name { get; set; }
}

[GenerateSerializer]
public partial class Dog : Animal
{
    public int BarkPitch { get; set; }
}

[GenerateSerializer]
public partial class Cat : Animal
{
    public bool HasClaws { get; set; }
}

[GenerateSerializer]
public partial class PetOwner
{
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(Dog))]
    [PolymorphicOption((byte)2, typeof(Cat))]
    public Animal? Pet { get; set; }
}

/*
[GenerateSerializer]
public partial class Zoo
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(Dog))]
    [PolymorphicOption((byte)2, typeof(Cat))]
    public List<Animal>? Animals { get; set; }
}
*/

public class PolymorphicTests
{
    [Fact]
    public void PolymorphicObject_Dog_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new PetOwner { Pet = new Dog { Name = "Fido", BarkPitch = 10 } };

        // Act
        var buffer = new byte[PetOwner.GetPacketSize(original)];
        PetOwner.Serialize(original, buffer);
        var deserialized = PetOwner.Deserialize(buffer);

        // Assert
        Assert.NotNull(deserialized.Pet);
        Assert.IsType<Dog>(deserialized.Pet);
        var dog = deserialized.Pet as Dog;
        Assert.NotNull(dog);
        Assert.Equal("Fido", dog.Name);
        Assert.Equal(10, dog.BarkPitch);
    }

    [Fact]
    public void PolymorphicObject_Cat_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new PetOwner { Pet = new Cat { Name = "Whiskers", HasClaws = true } };

        // Act
        var buffer = new byte[PetOwner.GetPacketSize(original)];
        PetOwner.Serialize(original, buffer);
        var deserialized = PetOwner.Deserialize(buffer);

        // Assert
        Assert.NotNull(deserialized.Pet);
        Assert.IsType<Cat>(deserialized.Pet);
        var cat = deserialized.Pet as Cat;
        Assert.NotNull(cat);
        Assert.Equal("Whiskers", cat.Name);
        Assert.True(cat.HasClaws);
    }

    /*
    [Fact]
    public void PolymorphicCollection_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new Zoo
        {
            Animals = new List<Animal>
            {
                new Dog { Name = "Buddy", BarkPitch = 5 },
                new Cat { Name = "Lucy", HasClaws = false }
            }
        };

        // Act
        var buffer = new byte[Zoo.GetPacketSize(original)];
        Zoo.Serialize(original, buffer);
        var deserialized = Zoo.Deserialize(buffer);

        // Assert
        Assert.NotNull(deserialized.Animals);
        Assert.Equal(2, deserialized.Animals.Count);

        Assert.IsType<Dog>(deserialized.Animals[0]);
        var dog = deserialized.Animals[0] as Dog;
        Assert.NotNull(dog);
        Assert.Equal("Buddy", dog.Name);
        Assert.Equal(5, dog.BarkPitch);

        Assert.IsType<Cat>(deserialized.Animals[1]);
        var cat = deserialized.Animals[1] as Cat;
        Assert.NotNull(cat);
        Assert.Equal("Lucy", cat.Name);
        Assert.False(cat.HasClaws);
    }
    */
}
