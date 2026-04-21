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
}
