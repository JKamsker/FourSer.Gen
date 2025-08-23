namespace FourSer.Tests.GeneratorTestCases.CollectionWithConstSize;

[GenerateSerializer]
public partial class EnumerableOfReferenceTypes
{
    
    [SerializeCollection(CountSize = 10)]
    public List<Entity> MyList { get; set; } 
}

[GenerateSerializer]
public partial class Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
