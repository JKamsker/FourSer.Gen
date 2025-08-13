using Serializer.Contracts;
using Serializer.Consumer.Extensions;
using System.Collections.Generic;

namespace TestNamespace;

[GenerateSerializer]
public partial class IndividualTypeIdsTest
{
    [SerializeCollection(CountType = typeof(byte))]
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