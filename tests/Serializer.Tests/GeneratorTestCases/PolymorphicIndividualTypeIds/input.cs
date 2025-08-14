namespace Serializer.Tests.GeneratorTestCases.PolymorphicIndividualTypeIds;

[GenerateSerializer]
public partial class PolymorphicIndividualTest
{
    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.IndividualTypeIds,
        TypeIdType = typeof(byte),
        CountType = typeof(byte)
    )]
    [PolymorphicOption(1, typeof(Cat))]
    [PolymorphicOption(2, typeof(Dog))]
    public List<Animal> Animals { get; set; } = new();
}

[GenerateSerializer]
public partial class Cat : Animal
{
    public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class Dog : Animal
{
    public string Breed { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class Animal
{
    public int Id { get; set; }
}