using System.Collections.Immutable;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Polymorphism.FullSupport;

public interface IAnimal
{
}

[GenerateSerializer]
public partial class DogAnimal : IAnimal
{
    public string Name { get; set; } = string.Empty;
    public int BarkPitch { get; set; }
}

[GenerateSerializer]
public partial class CatAnimal : IAnimal
{
    public string Name { get; set; } = string.Empty;
    public int Lives { get; set; }
}

[GenerateSerializer]
public partial class InterfacePetOwner
{
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IAnimal Pet { get; set; } = null!;
}

[GenerateSerializer]
public partial class InterfaceCollectionPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public List<IAnimal> ListAnimals { get; set; } = new();

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public ICollection<IAnimal> CollectionAnimals { get; set; } = new List<IAnimal>();

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IEnumerable<IAnimal> EnumerableAnimals { get; set; } = Array.Empty<IAnimal>();

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IReadOnlyCollection<IAnimal> ReadOnlyCollectionAnimals { get; set; } = Array.Empty<IAnimal>();

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IReadOnlyList<IAnimal> ReadOnlyListAnimals { get; set; } = Array.Empty<IAnimal>();
}

[GenerateSerializer]
public partial class ImmutableCollectionPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public ImmutableList<IAnimal> ListAnimals { get; set; } = ImmutableList<IAnimal>.Empty;

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public ImmutableArray<IAnimal> ArrayAnimals { get; set; } = ImmutableArray<IAnimal>.Empty;

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public ImmutableQueue<IAnimal> QueueAnimals { get; set; } = ImmutableQueue<IAnimal>.Empty;

    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public ImmutableStack<IAnimal> StackAnimals { get; set; } = ImmutableStack<IAnimal>.Empty;
}

[GenerateSerializer]
public partial class DefaultedSingleTypeIdPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal), isDefault: true)]
    public IReadOnlyCollection<IAnimal>? Animals { get; set; } = Array.Empty<IAnimal>();
}

[GenerateSerializer]
public partial class NonDefaultSingleTypeIdPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public List<IAnimal> Animals { get; set; } = new();
}

[GenerateSerializer]
public partial class DefaultedTypeIdPropertyPacket
{
    public byte AnimalType { get; set; }

    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.SingleTypeId,
        TypeIdProperty = nameof(AnimalType),
        TypeIdType = typeof(byte),
        CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal), isDefault: true)]
    public IReadOnlyCollection<IAnimal>? Animals { get; set; } = Array.Empty<IAnimal>();
}

public class PolymorphicCollectionParityTests
{
    [Fact]
    public void InterfacePolymorphicMember_ShouldRoundtrip()
    {
        var original = new InterfacePetOwner
        {
            Pet = new DogAnimal
            {
                Name = "Fido",
                BarkPitch = 7
            }
        };

        var buffer = new byte[InterfacePetOwner.GetPacketSize(original)];
        InterfacePetOwner.Serialize(original, buffer);

        var roundTripped = InterfacePetOwner.Deserialize(buffer);
        AssertAnimalSequence(
            [original.Pet],
            [roundTripped.Pet]);
    }

    [Fact]
    public void InterfacePolymorphicCollections_ShouldRoundtrip()
    {
        var animals = CreateAnimalSequence();
        var original = new InterfaceCollectionPacket
        {
            ListAnimals = animals.ToList(),
            CollectionAnimals = new List<IAnimal>(animals),
            EnumerableAnimals = animals.ToList(),
            ReadOnlyCollectionAnimals = animals.ToList(),
            ReadOnlyListAnimals = animals.ToList()
        };

        var buffer = new byte[InterfaceCollectionPacket.GetPacketSize(original)];
        InterfaceCollectionPacket.Serialize(original, buffer);

        var roundTripped = InterfaceCollectionPacket.Deserialize(buffer);

        AssertAnimalSequence(original.ListAnimals, roundTripped.ListAnimals);
        AssertAnimalSequence(original.CollectionAnimals, roundTripped.CollectionAnimals);
        AssertAnimalSequence(original.EnumerableAnimals, roundTripped.EnumerableAnimals);
        AssertAnimalSequence(original.ReadOnlyCollectionAnimals, roundTripped.ReadOnlyCollectionAnimals);
        AssertAnimalSequence(original.ReadOnlyListAnimals, roundTripped.ReadOnlyListAnimals);
    }

    [Fact]
    public void ImmutablePolymorphicCollections_ShouldRoundtrip()
    {
        var animals = CreateAnimalSequence();
        var original = new ImmutableCollectionPacket
        {
            ListAnimals = ImmutableList.CreateRange(animals),
            ArrayAnimals = ImmutableArray.CreateRange(animals),
            QueueAnimals = ImmutableQueue.CreateRange(animals),
            StackAnimals = ImmutableStack.CreateRange(animals)
        };

        var buffer = new byte[ImmutableCollectionPacket.GetPacketSize(original)];
        ImmutableCollectionPacket.Serialize(original, buffer);

        var roundTripped = ImmutableCollectionPacket.Deserialize(buffer);

        AssertAnimalSequence(original.ListAnimals, roundTripped.ListAnimals);
        AssertAnimalSequence(original.ArrayAnimals, roundTripped.ArrayAnimals);
        AssertAnimalSequence(original.QueueAnimals, roundTripped.QueueAnimals);
        AssertAnimalSequence(original.StackAnimals, roundTripped.StackAnimals);
    }

    [Fact]
    public void EmptySingleTypeIdCollection_ShouldUseDefaultDiscriminator()
    {
        var original = new DefaultedSingleTypeIdPacket
        {
            Animals = Array.Empty<IAnimal>()
        };

        var buffer = new byte[DefaultedSingleTypeIdPacket.GetPacketSize(original)];
        DefaultedSingleTypeIdPacket.Serialize(original, buffer);

        Assert.Equal(0, buffer[0]);
        Assert.Equal(20, buffer[1]);
    }

    [Fact]
    public void NullSingleTypeIdCollection_ShouldUseDefaultDiscriminator()
    {
        var original = new DefaultedSingleTypeIdPacket
        {
            Animals = null
        };

        var buffer = new byte[DefaultedSingleTypeIdPacket.GetPacketSize(original)];
        DefaultedSingleTypeIdPacket.Serialize(original, buffer);

        Assert.Equal(0, buffer[0]);
        Assert.Equal(20, buffer[1]);
    }

    [Fact]
    public void NonEmptySingleTypeIdCollectionWithoutDefault_ShouldRoundtrip()
    {
        var animals = CreateDogSequence().ToList();
        var original = new NonDefaultSingleTypeIdPacket
        {
            Animals = animals
        };

        var buffer = new byte[NonDefaultSingleTypeIdPacket.GetPacketSize(original)];
        NonDefaultSingleTypeIdPacket.Serialize(original, buffer);

        Assert.Equal(2, buffer[0]);
        Assert.Equal(10, buffer[1]);

        var roundTripped = NonDefaultSingleTypeIdPacket.Deserialize(buffer);
        AssertAnimalSequence(animals, roundTripped.Animals);
    }

    [Fact]
    public void NonIndexableSingleTypeIdTypeIdProperty_ShouldRoundtrip()
    {
        var animals = CreateDogSequence();
        var original = new DefaultedTypeIdPropertyPacket
        {
            Animals = animals
        };

        var buffer = new byte[DefaultedTypeIdPropertyPacket.GetPacketSize(original)];
        DefaultedTypeIdPropertyPacket.Serialize(original, buffer);

        var roundTripped = DefaultedTypeIdPropertyPacket.Deserialize(buffer);

        Assert.Equal(10, roundTripped.AnimalType);
        AssertAnimalSequence(animals, roundTripped.Animals);
    }

    [Fact]
    public void EmptyNonIndexableSingleTypeIdTypeIdProperty_ShouldUseDefaultDiscriminator()
    {
        var original = new DefaultedTypeIdPropertyPacket
        {
            Animals = Array.Empty<IAnimal>()
        };

        var buffer = new byte[DefaultedTypeIdPropertyPacket.GetPacketSize(original)];
        DefaultedTypeIdPropertyPacket.Serialize(original, buffer);

        Assert.Equal(20, buffer[0]);
        Assert.Equal(0, buffer[1]);
    }

    [Fact]
    public void NullPolymorphicMember_ShouldStillThrow()
    {
        var original = new InterfacePetOwner
        {
            Pet = null!
        };

        Assert.Throws<NullReferenceException>(() => InterfacePetOwner.Serialize(original, new MemoryStream()));
    }

    [Fact]
    public void NullPolymorphicCollectionItem_ShouldStillThrow()
    {
        var original = new InterfaceCollectionPacket
        {
            ListAnimals = [CreateAnimalSequence()[0], null!]
        };

        Assert.Throws<NullReferenceException>(() => InterfaceCollectionPacket.Serialize(original, new MemoryStream()));
    }

    private static IAnimal[] CreateAnimalSequence()
    {
        return
        [
            new DogAnimal { Name = "Fido", BarkPitch = 7 },
            new CatAnimal { Name = "Mochi", Lives = 9 }
        ];
    }

    private static IReadOnlyCollection<IAnimal> CreateDogSequence()
    {
        return
        [
            new DogAnimal { Name = "Rex", BarkPitch = 4 },
            new DogAnimal { Name = "Bolt", BarkPitch = 8 }
        ];
    }

    private static void AssertAnimalSequence(IEnumerable<IAnimal> expected, IEnumerable<IAnimal> actual)
    {
        var expectedDescriptors = expected.Select(DescribeAnimal).ToArray();
        var actualDescriptors = actual.Select(DescribeAnimal).ToArray();

        Assert.Equal(expectedDescriptors, actualDescriptors);
    }

    private static (string Type, string Name, int Value) DescribeAnimal(IAnimal animal)
    {
        return animal switch
        {
            DogAnimal dog => ("Dog", dog.Name, dog.BarkPitch),
            CatAnimal cat => ("Cat", cat.Name, cat.Lives),
            _ => throw new InvalidOperationException($"Unexpected animal type '{animal.GetType().FullName}'.")
        };
    }
}
