using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial class TestPacket1
{
    public int A { get; set; }
    public string B { get; set; } = string.Empty;
    [SerializeCollection]
    public List<int> C { get; set; } = new();
}

public class TestPacket1Test
{
    public void LoginAckPacketTest()
    {
        var original = new TestPacket1
        {
            A = 1,
            B = "Hello",
            C = new List<int> { 1, 2, 3 }
        };

        var size = TestPacket1.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestPacket1.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestPacket1.Deserialize(readOnlySpan);

        Assert.AreEqual(original.A, deserialized.A);
        Assert.AreEqual(original.B, deserialized.B);
        Assert.AreEqual(true, original.C.SequenceEqual(deserialized.C));
    }
}
