using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class TestWithCountType
{
   [SerializeCollection(CountType = typeof(ushort))]
   public List<int> MyList { get; set; } = new();
}

public class TestWithCountTypeTest
{
    [Fact]
    public void MyPacketWithCountTypeTest()
    {
        var original = new TestWithCountType
        {
            MyList = new List<int> { 10, 20, 30, 40, 50 }
        };

        var size = TestWithCountType.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithCountType.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithCountType.Deserialize(readOnlySpan);

        Assert.True(original.MyList.SequenceEqual(deserialized.MyList));
    }
}