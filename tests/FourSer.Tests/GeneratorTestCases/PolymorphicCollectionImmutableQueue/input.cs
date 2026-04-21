using System.Collections.Immutable;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionImmutableQueue;

[GenerateSerializer]
public partial class Inventory
{
    public byte ItemType { get; set; }

    [SerializeCollection(
        PolymorphicMode = PolymorphicMode.SingleTypeId,
        TypeIdProperty = nameof(ItemType),
        TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public ImmutableQueue<IItem> Items { get; set; } = ImmutableQueue<IItem>.Empty;
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

[GenerateSerializer]
public partial class Potion : IItem
{
}
