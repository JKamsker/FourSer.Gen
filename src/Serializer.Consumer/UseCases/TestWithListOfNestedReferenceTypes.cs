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