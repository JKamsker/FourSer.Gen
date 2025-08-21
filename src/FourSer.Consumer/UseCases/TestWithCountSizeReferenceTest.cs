using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial class TestWithCountSizeReference
{
    public ushort MyListCount { get; set; }

    [SerializeCollection(CountSizeReference = "MyListCount")]
    public List<int> MyList { get; set; } = new();
}

public class TestWithCountSizeReferenceTest
{
    public void MyPacketWithCountSizeReferenceTest()
    {
        var original = new TestWithCountSizeReference
        {
            MyList = new List<int> { 100, 200, 300 }
        };
        original.MyListCount = (ushort)original.MyList.Count;

        var size = TestWithCountSizeReference.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithCountSizeReference.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithCountSizeReference.Deserialize(readOnlySpan);

        Assert.AreEqual(original.MyListCount, deserialized.MyListCount);
        Assert.AreEqual(true, original.MyList.SequenceEqual(deserialized.MyList));
    }
}
