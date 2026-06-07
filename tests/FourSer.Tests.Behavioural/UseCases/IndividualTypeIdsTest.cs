using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases
{
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class IndividualTypeIdsTest
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

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class Animal
    {
        public int Age { get; set; }
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class Cat : Animal
    {
        public string Name { get; set; } = string.Empty;
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class Dog : Animal
    {
        public int Weight { get; set; }
    }
}