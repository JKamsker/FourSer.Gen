namespace FourSer.Tests.GeneratorTestCases.Zoo;

[GenerateSerializer]
public partial class Zoo
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(Dog))]
    [PolymorphicOption((byte)2, typeof(Cat))]
    public List<Animal>? Animals { get; set; }
}

[GenerateSerializer]
public partial class Animal
{
    public int Age { get; set; }
}

[GenerateSerializer]
public partial class Cat : Animal
{
    public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class Dog : Animal
{
    public int Weight { get; set; }
}
