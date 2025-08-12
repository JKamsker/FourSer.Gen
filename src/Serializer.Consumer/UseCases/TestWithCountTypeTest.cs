using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class TestWithCountType
{
   [SerializeCollection(CountType = typeof(ushort))]
   public List<int> MyList { get; set; } = new();
}

public class TestWithCountTypeTest
{
    public void MyPacketWithCountTypeTest()
    {
        var original = new TestWithCountType
        {
            MyList = new List<int> { 10, 20, 30, 40, 50 }
        };

        var size = Consumer.TestWithCountType.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        Consumer.TestWithCountType.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = Consumer.TestWithCountType.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(true, original.MyList.SequenceEqual(deserialized.MyList));
    }
}