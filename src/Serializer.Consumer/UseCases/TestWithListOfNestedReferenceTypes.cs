using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

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
        var deserialized = TestWithListOfNestedReferenceTypes.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(original.MyList.Count, deserialized.MyList.Count);
        for (int i = 0; i < original.MyList.Count; i++)
        {
            Assert.AreEqual(original.MyList[i].Id, deserialized.MyList[i].Id);
            Assert.AreEqual(original.MyList[i].Age, deserialized.MyList[i].Age);
            Assert.AreEqual(original.MyList[i].Name, deserialized.MyList[i].Name);
        }
    }
}