using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases
{
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class SingleTypeIdTest
    {
        public long AnimalType { get; set; }
        [SerializeCollection(TypeIdProperty = "AnimalType", PolymorphicMode = PolymorphicMode.SingleTypeId)]
        [PolymorphicOption((long)1, typeof(CatBase))]
        [PolymorphicOption((long)2, typeof(DogBase))]
        public List<AnimalBase> Animals { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class AnimalBase
    {
        public int Age { get; set; }
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class CatBase : AnimalBase
    {
        public string Name { get; set; } = string.Empty;
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class DogBase : AnimalBase
    {
        public int Weight { get; set; }
    }
}
