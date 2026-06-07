using System.Buffers;
using System.IO.Pipelines;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Interop;

[GenerateSerializer]
public partial class TransportNestedPacket
{
    public int Code { get; set; }
}

[GenerateSerializer(
    SerializerGenerationMethods.BufferWriter
    | SerializerGenerationMethods.SequenceReader
    | SerializerGenerationMethods.PipeWriter
    | SerializerGenerationMethods.PipeReader)]
public partial class TransportOverloadPacket
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [SerializeCollection]
    public List<byte> Data { get; set; } = new();

    public TransportNestedPacket Nested { get; set; } = new();
}

public class TransportOverloadTests
{
    [Fact]
    public void BufferWriterAndSequenceReader_ShouldRoundTrip()
    {
        var packet = CreatePacket();
        var writer = new ArrayBufferWriter<byte>();

        TransportOverloadPacket.Serialize(packet, writer);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var reader = new SequenceReader<byte>(sequence);

        var roundTripped = TransportOverloadPacket.Deserialize(ref reader);

        AssertPacket(packet, roundTripped);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void PipeWriterAndPipeReader_ShouldRoundTrip()
    {
        var packet = CreatePacket();
        var pipe = new Pipe();

        TransportOverloadPacket.Serialize(packet, pipe.Writer);
        pipe.Writer.Complete();

        var roundTripped = TransportOverloadPacket.Deserialize(pipe.Reader);
        pipe.Reader.Complete();

        AssertPacket(packet, roundTripped);
    }

    private static TransportOverloadPacket CreatePacket()
    {
        return new TransportOverloadPacket
        {
            Id = 42,
            Name = "transport",
            Data = [1, 2, 3, 4],
            Nested = new TransportNestedPacket { Code = 7 },
        };
    }

    private static void AssertPacket(TransportOverloadPacket expected, TransportOverloadPacket actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.Nested.Code, actual.Nested.Code);
    }
}
