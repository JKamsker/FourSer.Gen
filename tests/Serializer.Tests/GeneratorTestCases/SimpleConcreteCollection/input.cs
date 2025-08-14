namespace Serializer.Tests.GeneratorTestCases.SimpleConcreteCollection;

[GenerateSerializer]
public partial class SimpleConcreteCollectionTest
{
    [SerializeCollection]
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