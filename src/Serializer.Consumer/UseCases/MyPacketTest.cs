using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class MyPacket
{
    [SerializeCollection]
    public List<byte> Data { get; set; } = new();
}

public class MyPacketTest
{
    public void MyPacketImplicitCountTest()
    {
        var original = new MyPacket
        {
            Data = new List<byte> { 0xDE, 0xAD, 0xBE, 0xEF }
        };

        var size = Consumer.MyPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        Consumer.MyPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = Consumer.MyPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(true, original.Data.SequenceEqual(deserialized.Data));
    }
}