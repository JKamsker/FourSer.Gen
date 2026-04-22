using System.Collections;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class ByteCountPacket
{
    [SerializeCollection(CountType = typeof(byte))]
    public List<int> Values { get; set; } = new();
}

[GenerateSerializer]
public partial class StackRoundTripPacket
{
    [SerializeCollection]
    public Stack<int> Values { get; set; } = new();
}

[GenerateSerializer]
public partial class CountSizeReferenceEnumerablePacket
{
    public byte Count { get; set; }

    [SerializeCollection(CountSizeReference = nameof(Count))]
    public IEnumerable<int> Values { get; set; } = Enumerable.Empty<int>();
}

[GenerateSerializer]
public partial class ImmutablePolymorphicPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(RegressionDog))]
    [PolymorphicOption((byte)2, typeof(RegressionCat))]
    public ImmutableArray<IRegressionAnimal> Animals { get; set; } = ImmutableArray<IRegressionAnimal>.Empty;
}

public interface IRegressionAnimal;

[GenerateSerializer]
public partial class RegressionDog : IRegressionAnimal;

[GenerateSerializer]
public partial class RegressionCat : IRegressionAnimal;

public class CollectionGenerationRegressionTests
{
    [Fact]
    public void NarrowCountCollections_ShouldThrowInsteadOfTruncating()
    {
        var original = new ByteCountPacket
        {
            Values = Enumerable.Range(0, byte.MaxValue + 1).ToList()
        };

        Assert.Throws<OverflowException>(() => ByteCountPacket.GetPacketSize(original));

        var buffer = new byte[byte.MaxValue * sizeof(int) + sizeof(byte)];
        Assert.Throws<OverflowException>(() => ByteCountPacket.Serialize(original, buffer));

        using var stream = new MemoryStream();
        Assert.Throws<OverflowException>(() => ByteCountPacket.Serialize(original, stream));
    }

    [Fact]
    public void StackCollections_ShouldPreserveEnumerationOrderOnRoundtrip()
    {
        var original = new StackRoundTripPacket();
        original.Values.Push(1);
        original.Values.Push(2);
        original.Values.Push(3);

        var buffer = new byte[StackRoundTripPacket.GetPacketSize(original)];
        StackRoundTripPacket.Serialize(original, buffer);

        var roundTripped = StackRoundTripPacket.Deserialize(buffer);
        Assert.Equal(original.Values.ToArray(), roundTripped.Values.ToArray());

        using var stream = new MemoryStream();
        StackRoundTripPacket.Serialize(original, stream);
        stream.Position = 0;

        var streamRoundTripped = StackRoundTripPacket.Deserialize(stream);
        Assert.Equal(original.Values.ToArray(), streamRoundTripped.Values.ToArray());
    }

    [Fact]
    public void CountSizeReferenceEnumerables_ShouldOnlyEnumerateOnceDuringPacketSizing()
    {
        var packet = new CountSizeReferenceEnumerablePacket
        {
            Values = new SinglePassEnumerable<int>(Enumerable.Range(1, 3))
        };

        var size = CountSizeReferenceEnumerablePacket.GetPacketSize(packet);

        Assert.Equal(sizeof(byte) + (3 * sizeof(int)), size);
    }

    [Fact]
    public void CountSizeReferenceEnumerables_ShouldOverwriteStaleSourceCount()
    {
        var packet = new CountSizeReferenceEnumerablePacket
        {
            Count = 99,
            Values = Enumerable.Range(1, 3).ToArray()
        };

        var buffer = new byte[CountSizeReferenceEnumerablePacket.GetPacketSize(packet)];
        CountSizeReferenceEnumerablePacket.Serialize(packet, buffer);

        Assert.Equal((byte)3, buffer[0]);

        var roundTripped = CountSizeReferenceEnumerablePacket.Deserialize(buffer);
        Assert.Equal((byte)3, roundTripped.Count);
        Assert.Equal([1, 2, 3], roundTripped.Values.ToArray());

        using var stream = new MemoryStream();
        CountSizeReferenceEnumerablePacket.Serialize(packet, stream);
        Assert.Equal((byte)3, stream.ToArray()[0]);
    }

    [Fact]
    public void PolymorphicImmutableCollections_ShouldThrowForUnsupportedTypes()
    {
        var packet = new ImmutablePolymorphicPacket
        {
            Animals = ImmutableArray.Create<IRegressionAnimal>(new RegressionDog(), new RegressionLizard())
        };

        var buffer = new byte[16];
        Assert.Throws<InvalidDataException>(() => ImmutablePolymorphicPacket.Serialize(packet, buffer));

        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => ImmutablePolymorphicPacket.Serialize(packet, stream));
    }

    private sealed class RegressionLizard : IRegressionAnimal;

    private sealed class SinglePassEnumerable<T>(IEnumerable<T> source) : IEnumerable<T>
    {
        private bool _enumerated;

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerated)
            {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            _enumerated = true;
            return source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
