using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Optimization;

[GenerateSerializer]
public partial class ListFastPathPacket
{
    [SerializeCollection]
    public List<int> Values { get; set; } = new();
}

public interface IHoistedAnimal
{
}

[GenerateSerializer]
public partial class HoistedDog : IHoistedAnimal
{
    public int Speed { get; set; }
}

[GenerateSerializer]
public partial class HoistedCat : IHoistedAnimal
{
    public int Lives { get; set; }
}

[GenerateSerializer]
public partial class HoistedPolymorphicPacket
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(HoistedDog))]
    [PolymorphicOption((byte)2, typeof(HoistedCat), isDefault: true)]
    public IReadOnlyCollection<IHoistedAnimal> Animals { get; set; } = Array.Empty<IHoistedAnimal>();
}
