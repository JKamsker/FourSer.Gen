namespace Serializer.Tests.GeneratorTestCases.CollectionULongCount;

[GenerateSerializer]
public partial class ULongCountTest
{
    [SerializeCollection(CountType = typeof(ulong))]
    public List<Cat> Cats { get; set; } = new();
}

[GenerateSerializer]
public partial class Cat : Animal
{
    public string Name { get; set; } = string.Empty;
}
public abstract class Animal
{
    public int Id { get; set; }
}