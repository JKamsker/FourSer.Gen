using System.Collections.Generic;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicDefaultOption;

[GenerateSerializer]
public partial class Inventory
{
    public byte ItemType { get; set; }

    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield), isDefault: true)]
    public List<IItem> Items { get; set; } = new();

    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.SingleTypeId,
        TypeIdProperty = nameof(ItemType),
        TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield), isDefault: true)]
    public IReadOnlyCollection<IItem> ReadOnlyItems { get; set; } = new List<IItem>();
}

public interface IItem
{
}

[GenerateSerializer]
public partial class Sword : IItem
{
}

[GenerateSerializer]
public partial class Shield : IItem
{
}
