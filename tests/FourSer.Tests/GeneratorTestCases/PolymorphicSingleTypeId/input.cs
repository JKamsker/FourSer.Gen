namespace FourSer.Tests.GeneratorTestCases.PolymorphicSingleTypeId;

[GenerateSerializer]
public partial class PolymorphicSingleTest
{
    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.SingleTypeId,
        TypeIdType = typeof(byte),
        CountType = typeof(ushort)
    )]
    [PolymorphicOption((byte)1, typeof(Cat))]
    [PolymorphicOption((byte)2, typeof(Dog))]
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