using FourSer.Contracts;

namespace FourSer.Consumer.UseCases
{
    [GenerateSerializer]
    public partial class SingleTypeIdTest
    {
        public byte AnimalType { get; set; }

        [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), TypeIdProperty = nameof(AnimalType))]
        [PolymorphicOption((byte)1, typeof(CatBase))]
        [PolymorphicOption((byte)2, typeof(DogBase))]
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
