using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class CXEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class TestWithListOfReferenceTypes
{
    [SerializeCollection]
    public List<CXEntity> MyList { get; set; } = new();
}

public class TestWithListOfReferenceTypesTest
{
    [Fact]
    public void ListOfReferenceTypesTest()
    {
        var original = new TestWithListOfReferenceTypes
        {
            MyList = new List<CXEntity>
            {
                new CXEntity { Id = 1, Name = "Entity1" },
                new CXEntity { Id = 2, Name = "Entity2" },
                new CXEntity { Id = 3, Name = "Entity3" }
            }
        };

        var size = TestWithListOfReferenceTypes.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithListOfReferenceTypes.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithListOfReferenceTypes.Deserialize(readOnlySpan);

        Assert.Equal(original.MyList.Count, deserialized.MyList.Count);
        for (int i = 0; i < original.MyList.Count; i++)
        {
            Assert.Equal(original.MyList[i].Id, deserialized.MyList[i].Id);
            Assert.Equal(original.MyList[i].Name, deserialized.MyList[i].Name);
        }
    }
}