using FourSer.Tests.Behavioural.Batching;
using FourSer.Tests.Behavioural.Infrastructure;

namespace FourSer.Tests.Behavioural.Optimization;

public class OptimizationBehaviourTests
{
    [Fact]
    public void LargeBatchedStreamWrites_ShouldUseSingleLargeWrite()
    {
        var packet = new LargeBatchWritePacket();
        for (var i = 1; i <= 65; i++)
        {
            typeof(LargeBatchWritePacket).GetProperty($"Value{i:00}")!.SetValue(packet, i);
        }

        using var stream = new TrackingWriteStream();
        LargeBatchWritePacket.Serialize(packet, stream);

        Assert.Equal(1, stream.WriteCallCount);
        Assert.True(stream.LargestWriteBytes > 256);
    }

    [Fact]
    public void FusedStreamStrings_ShouldWriteLengthAndPayloadTogether()
    {
        var packet = new FusedStringPacket
        {
            Value = "fused-string-payload"
        };

        using var stream = new TrackingWriteStream();
        FusedStringPacket.Serialize(packet, stream);

        Assert.Equal(1, stream.WriteCallCount);
        Assert.Equal(FusedStringPacket.GetPacketSize(packet), stream.ToArray().Length);
    }

    [Fact]
    public void ListFastPathDeserialization_ShouldUseCollectionsMarshalSetCount()
    {
        var source = GeneratedSourceFiles.Read("FourSer_Tests_Behavioural_Optimization_ListFastPathPacket.g.cs");

        Assert.Contains("CollectionsMarshal.SetCount", source);
        Assert.Contains("MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(valuesValue))", source);
    }

    [Fact]
    public void PolymorphicHeaderHoisting_ShouldWriteDiscriminatorOnce()
    {
        var source = GeneratedSourceFiles.Read("FourSer_Tests_Behavioural_Optimization_HoistedPolymorphicPacket.g.cs");

        Assert.Contains("var discriminator = animalsEnumerator.Current switch", source);
        Assert.Contains("SpanWriter.WriteByte(ref data, (byte)(discriminator));", source);
        Assert.Contains("switch (discriminator)", source);
        Assert.Equal(1, CountOccurrences(source, "SpanWriter.WriteByte(ref data, (byte)(discriminator));"));
        Assert.Equal(1, CountOccurrences(source, "StreamWriter.WriteByte(stream, (byte)(discriminator));"));
    }

    [Fact]
    public void FixedCollectionBatchingWithFieldsOnBothSides_ShouldBatchIntoSingleWrite()
    {
        var packet = new BatchTestFixedCollectionSandwich
        {
            Before1 = 10,
            Before2 = 20,
            Middle = [1, 2, 3, 4],
            After1 = 30,
            After2 = 40,
        };

        using var stream = new TrackingWriteStream();
        BatchTestFixedCollectionSandwich.Serialize(packet, stream);

        Assert.Equal(1, stream.WriteCallCount);
        Assert.Equal(BatchTestFixedCollectionSandwich.GetPacketSize(packet), stream.ToArray().Length);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
