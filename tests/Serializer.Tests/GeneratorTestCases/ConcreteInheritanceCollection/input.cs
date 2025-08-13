using Serializer.Contracts;
using Serializer.Consumer.Extensions;
using System.Collections.Generic;

namespace TestNamespace;

[GenerateSerializer]
public partial class ConcreteInheritanceTest
{
    [SerializeCollection]
    public List<Dog> Dogs { get; set; } = new();
}

[GenerateSerializer]
public partial class Dog : Pet
{
    public string Breed { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class Pet
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}