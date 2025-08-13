using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class MyPacket
{
    public List<byte> Data { get; set; } = new();
    public byte[]? Data1 { get; set; }
}

public class MyPacketTest
{
    public void MyPacketImplicitCountTest()
    {
        var original = new MyPacket
        {
            Data = new List<byte> { 0xDE, 0xAD, 0xBE, 0xEF }
        };

        var size = MyPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        MyPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = MyPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(true, original.Data.SequenceEqual(deserialized.Data));
    }
}