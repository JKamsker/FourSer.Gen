using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

[GenerateSerializer]
public partial class UIntCountTest
{
    [SerializeCollection(CountType = typeof(uint))]
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