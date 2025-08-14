namespace Serializer.Tests.GeneratorTestCases.CollectionUShortCount;

[GenerateSerializer]
public partial class UShortCountTest
{
    [SerializeCollection(CountType = typeof(ushort))]
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