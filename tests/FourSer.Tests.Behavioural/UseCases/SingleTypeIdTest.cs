using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases
{
    [GenerateSerializer]
    public partial class SingleTypeIdTest
    {
        public long AnimalType { get; set; }
        [SerializeCollection(TypeIdProperty = "AnimalType", PolymorphicMode = PolymorphicMode.SingleTypeId)]
        [PolymorphicOption((long)1, typeof(CatBase))]
        [PolymorphicOption((long)2, typeof(DogBase))]
        public List<AnimalBase> Animals { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class AnimalBase
    {
        public int Age { get; set; }
    }

    [GenerateSerializer]
    public partial class CatBase : AnimalBase
    {
        public string Name { get; set; } = string.Empty;
    }

    [GenerateSerializer]
    public partial class DogBase : AnimalBase
    {
        public int Weight { get; set; }
    }
}
