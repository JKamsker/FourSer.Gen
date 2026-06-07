using System.Collections.Immutable;
using FourSer.Contracts;
using FourSer.Tests.Behavioural.Infrastructure;

namespace FourSer.Tests.Behavioural.Polymorphism.FullSupport;

public interface IAnimal
{
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class DogAnimal : IAnimal
{
    public string Name { get; set; } = string.Empty;
    public int BarkPitch { get; set; }
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class CatAnimal : IAnimal
{
    public string Name { get; set; } = string.Empty;
    public int Lives { get; set; }
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class InterfacePetOwner
{
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IAnimal Pet { get; set; } = null!;
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
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

[GenerateSerializer(SerializerGenerationMethods.Stream)]
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

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class DefaultedSingleTypeIdPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal), isDefault: true)]
    public IReadOnlyCollection<IAnimal>? Animals { get; set; } = Array.Empty<IAnimal>();
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class NonDefaultSingleTypeIdPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public List<IAnimal> Animals { get; set; } = new();
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
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

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class SingleTypeIdListFailurePacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public List<IAnimal> Animals { get; set; } = new();
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class SingleTypeIdReadOnlyCollectionFailurePacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IReadOnlyCollection<IAnimal> Animals { get; set; } = Array.Empty<IAnimal>();
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class SingleTypeIdReadOnlyListFailurePacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IReadOnlyList<IAnimal> Animals { get; set; } = Array.Empty<IAnimal>();
}

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial class SingleTypeIdEnumerableFailurePacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(DogAnimal))]
    [PolymorphicOption((byte)20, typeof(CatAnimal))]
    public IEnumerable<IAnimal> Animals { get; set; } = Array.Empty<IAnimal>();
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

        var streamRoundTripped = RoundTripThroughStream(original, InterfacePetOwner.Serialize, InterfacePetOwner.Deserialize);
        AssertAnimalSequence(
            [original.Pet],
            [streamRoundTripped.Pet]);
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

        var streamRoundTripped = RoundTripThroughStream(original, InterfaceCollectionPacket.Serialize, InterfaceCollectionPacket.Deserialize);
        AssertAnimalSequence(original.ListAnimals, streamRoundTripped.ListAnimals);
        AssertAnimalSequence(original.CollectionAnimals, streamRoundTripped.CollectionAnimals);
        AssertAnimalSequence(original.EnumerableAnimals, streamRoundTripped.EnumerableAnimals);
        AssertAnimalSequence(original.ReadOnlyCollectionAnimals, streamRoundTripped.ReadOnlyCollectionAnimals);
        AssertAnimalSequence(original.ReadOnlyListAnimals, streamRoundTripped.ReadOnlyListAnimals);
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

        var streamRoundTripped = RoundTripThroughStream(original, ImmutableCollectionPacket.Serialize, ImmutableCollectionPacket.Deserialize);
        AssertAnimalSequence(original.ListAnimals, streamRoundTripped.ListAnimals);
        AssertAnimalSequence(original.ArrayAnimals, streamRoundTripped.ArrayAnimals);
        AssertAnimalSequence(original.QueueAnimals, streamRoundTripped.QueueAnimals);
        AssertAnimalSequence(original.StackAnimals, streamRoundTripped.StackAnimals);
    }

    [Fact]
    public void DefaultConstructedImmutablePolymorphicCollections_ShouldKeepEmptyCollections()
    {
        var original = new ImmutableCollectionPacket();

        Assert.NotNull(original.ListAnimals);
        Assert.Empty(original.ListAnimals);
        Assert.False(original.ArrayAnimals.IsDefault);
        Assert.Empty(original.ArrayAnimals);
        Assert.NotNull(original.QueueAnimals);
        Assert.Empty(original.QueueAnimals);
        Assert.NotNull(original.StackAnimals);
        Assert.Empty(original.StackAnimals);

        var buffer = new byte[ImmutableCollectionPacket.GetPacketSize(original)];
        ImmutableCollectionPacket.Serialize(original, buffer);

        var roundTripped = ImmutableCollectionPacket.Deserialize(buffer);

        Assert.NotNull(roundTripped.ListAnimals);
        Assert.Empty(roundTripped.ListAnimals);
        Assert.False(roundTripped.ArrayAnimals.IsDefault);
        Assert.Empty(roundTripped.ArrayAnimals);
        Assert.NotNull(roundTripped.QueueAnimals);
        Assert.Empty(roundTripped.QueueAnimals);
        Assert.NotNull(roundTripped.StackAnimals);
        Assert.Empty(roundTripped.StackAnimals);
    }

    [Fact]
    public void ExplicitlyDefaultedImmutableArray_ShouldSerializeAsEmpty()
    {
        var original = new ImmutableCollectionPacket
        {
            ArrayAnimals = default
        };

        var buffer = new byte[ImmutableCollectionPacket.GetPacketSize(original)];
        ImmutableCollectionPacket.Serialize(original, buffer);

        var roundTripped = ImmutableCollectionPacket.Deserialize(buffer);

        Assert.False(roundTripped.ArrayAnimals.IsDefault);
        Assert.Empty(roundTripped.ArrayAnimals);
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

        var streamRoundTripped = RoundTripThroughStream(original, DefaultedSingleTypeIdPacket.Serialize, DefaultedSingleTypeIdPacket.Deserialize);
        AssertAnimalSequence(original.Animals ?? Array.Empty<IAnimal>(), streamRoundTripped.Animals ?? Array.Empty<IAnimal>());
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

        var streamRoundTripped = RoundTripThroughStream(original, DefaultedSingleTypeIdPacket.Serialize, DefaultedSingleTypeIdPacket.Deserialize);
        AssertAnimalSequence(original.Animals ?? Array.Empty<IAnimal>(), streamRoundTripped.Animals ?? Array.Empty<IAnimal>());
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

        var streamRoundTripped = RoundTripThroughStream(original, NonDefaultSingleTypeIdPacket.Serialize, NonDefaultSingleTypeIdPacket.Deserialize);
        AssertAnimalSequence(animals, streamRoundTripped.Animals);
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
        AssertAnimalSequence(animals, roundTripped.Animals ?? Array.Empty<IAnimal>());

        var streamRoundTripped = RoundTripThroughStream(original, DefaultedTypeIdPropertyPacket.Serialize, DefaultedTypeIdPropertyPacket.Deserialize);
        Assert.Equal(10, streamRoundTripped.AnimalType);
        AssertAnimalSequence(animals, streamRoundTripped.Animals ?? Array.Empty<IAnimal>());
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

        var streamRoundTripped = RoundTripThroughStream(original, DefaultedTypeIdPropertyPacket.Serialize, DefaultedTypeIdPropertyPacket.Deserialize);
        Assert.Equal(20, streamRoundTripped.AnimalType);
        AssertAnimalSequence(original.Animals ?? Array.Empty<IAnimal>(), streamRoundTripped.Animals ?? Array.Empty<IAnimal>());
    }

    [Fact]
    public void ExplicitTypeIdPropertyOnEmptyCollection_ShouldIgnorePropertyValueAndUseDefault()
    {
        var original = new DefaultedTypeIdPropertyPacket
        {
            AnimalType = 10,
            Animals = Array.Empty<IAnimal>()
        };

        var buffer = new byte[DefaultedTypeIdPropertyPacket.GetPacketSize(original)];
        DefaultedTypeIdPropertyPacket.Serialize(original, buffer);

        Assert.Equal(20, buffer[0]);
        Assert.Equal(0, buffer[1]);

        var roundTripped = DefaultedTypeIdPropertyPacket.Deserialize(buffer);
        Assert.Equal(20, roundTripped.AnimalType);
        AssertAnimalSequence(original.Animals ?? Array.Empty<IAnimal>(), roundTripped.Animals ?? Array.Empty<IAnimal>());

        var streamRoundTripped = RoundTripThroughStream(original, DefaultedTypeIdPropertyPacket.Serialize, DefaultedTypeIdPropertyPacket.Deserialize);
        Assert.Equal(20, streamRoundTripped.AnimalType);
        AssertAnimalSequence(original.Animals ?? Array.Empty<IAnimal>(), streamRoundTripped.Animals ?? Array.Empty<IAnimal>());
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

    [Fact]
    public void SingleTypeIdCollections_ShouldRejectMixedTypesBeforeWritingCollectionBytes()
    {
        var mixedAnimals = CreateAnimalSequence();

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdListFailurePacket { Animals = mixedAnimals.ToList() },
            SingleTypeIdListFailurePacket.GetPacketSize,
            SingleTypeIdListFailurePacket.Serialize,
            SingleTypeIdListFailurePacket.Serialize,
            typeof(InvalidDataException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdReadOnlyCollectionFailurePacket { Animals = mixedAnimals },
            SingleTypeIdReadOnlyCollectionFailurePacket.GetPacketSize,
            SingleTypeIdReadOnlyCollectionFailurePacket.Serialize,
            SingleTypeIdReadOnlyCollectionFailurePacket.Serialize,
            typeof(InvalidDataException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdReadOnlyListFailurePacket { Animals = mixedAnimals },
            SingleTypeIdReadOnlyListFailurePacket.GetPacketSize,
            SingleTypeIdReadOnlyListFailurePacket.Serialize,
            SingleTypeIdReadOnlyListFailurePacket.Serialize,
            typeof(InvalidDataException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdEnumerableFailurePacket { Animals = mixedAnimals },
            SingleTypeIdEnumerableFailurePacket.GetPacketSize,
            SingleTypeIdEnumerableFailurePacket.Serialize,
            SingleTypeIdEnumerableFailurePacket.Serialize,
            typeof(InvalidDataException));
    }

    [Fact]
    public void SingleTypeIdCollections_ShouldRejectLateNullsBeforeWritingCollectionBytes()
    {
        var lateNullAnimals = CreateLateNullSequence();

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdListFailurePacket { Animals = lateNullAnimals.ToList() },
            SingleTypeIdListFailurePacket.GetPacketSize,
            SingleTypeIdListFailurePacket.Serialize,
            SingleTypeIdListFailurePacket.Serialize,
            typeof(NullReferenceException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdReadOnlyCollectionFailurePacket { Animals = lateNullAnimals },
            SingleTypeIdReadOnlyCollectionFailurePacket.GetPacketSize,
            SingleTypeIdReadOnlyCollectionFailurePacket.Serialize,
            SingleTypeIdReadOnlyCollectionFailurePacket.Serialize,
            typeof(NullReferenceException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdReadOnlyListFailurePacket { Animals = lateNullAnimals },
            SingleTypeIdReadOnlyListFailurePacket.GetPacketSize,
            SingleTypeIdReadOnlyListFailurePacket.Serialize,
            SingleTypeIdReadOnlyListFailurePacket.Serialize,
            typeof(NullReferenceException));

        AssertSingleTypeIdFailureBeforeWritingBytes(
            new SingleTypeIdEnumerableFailurePacket { Animals = lateNullAnimals },
            SingleTypeIdEnumerableFailurePacket.GetPacketSize,
            SingleTypeIdEnumerableFailurePacket.Serialize,
            SingleTypeIdEnumerableFailurePacket.Serialize,
            typeof(NullReferenceException));
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

    private static IAnimal[] CreateLateNullSequence()
    {
        return
        [
            new DogAnimal { Name = "Scout", BarkPitch = 5 },
            null!
        ];
    }

    private static T RoundTripThroughStream<T>(T original, Action<T, Stream> serialize, Func<Stream, T> deserialize)
    {
        using var stream = new MemoryStream();
        serialize(original, stream);
        stream.Position = 0;
        return deserialize(stream);
    }

    private static void AssertAnimalSequence(IEnumerable<IAnimal> expected, IEnumerable<IAnimal> actual)
    {
        var expectedDescriptors = expected.Select(DescribeAnimal).ToArray();
        var actualDescriptors = actual.Select(DescribeAnimal).ToArray();

        Assert.Equal(expectedDescriptors, actualDescriptors);
    }

    private static void AssertSingleTypeIdFailureBeforeWritingBytes<TPacket>(
        TPacket packet,
        Func<TPacket, int> getPacketSize,
        SpanSerialize<TPacket> serializeSpan,
        Action<TPacket, Stream> serializeStream,
        Type exceptionType)
    {
        Assert.Throws(exceptionType, () => getPacketSize(packet));

        var buffer = Enumerable.Repeat((byte)0xCC, 32).ToArray();
        Assert.Throws(exceptionType, () => serializeSpan(packet, buffer));
        Assert.All(buffer, value => Assert.Equal((byte)0xCC, value));

        using var trackingStream = new TrackingWriteStream();
        Assert.Throws(exceptionType, () => serializeStream(packet, trackingStream));
        Assert.Equal(0, trackingStream.WriteCallCount);
        Assert.Empty(trackingStream.ToArray());
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

    private delegate void SpanSerialize<in TPacket>(TPacket packet, Span<byte> buffer);
}
