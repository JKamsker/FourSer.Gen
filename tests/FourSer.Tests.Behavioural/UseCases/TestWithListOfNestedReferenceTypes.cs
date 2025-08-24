using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class TestWithListOfNestedReferenceTypes
{
    [SerializeCollection]
    public List<NestedEntity> MyList { get; set; } = new();
    
    [GenerateSerializer]
    public partial class NestedEntity
    {
        public int Id { get; set; }
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

public class TestWithListOfNestedReferenceTypesTest
{
    [Fact]
    public void ListOfNestedReferenceTypesTest()
    {
        var original = new TestWithListOfNestedReferenceTypes
        {
            MyList = new List<TestWithListOfNestedReferenceTypes.NestedEntity>
            {
                new TestWithListOfNestedReferenceTypes.NestedEntity { Id = 1, Age = 25, Name = "Alice" },
                new TestWithListOfNestedReferenceTypes.NestedEntity { Id = 2, Age = 30, Name = "Bob" },
                new TestWithListOfNestedReferenceTypes.NestedEntity { Id = 3, Age = 28, Name = "Charlie" }
            }
        };

        var size = TestWithListOfNestedReferenceTypes.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithListOfNestedReferenceTypes.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithListOfNestedReferenceTypes.Deserialize(readOnlySpan);

        Assert.Equal(original.MyList.Count, deserialized.MyList.Count);
        for (int i = 0; i < original.MyList.Count; i++)
        {
            Assert.Equal(original.MyList[i].Id, deserialized.MyList[i].Id);
            Assert.Equal(original.MyList[i].Age, deserialized.MyList[i].Age);
            Assert.Equal(original.MyList[i].Name, deserialized.MyList[i].Name);
        }
    }
}