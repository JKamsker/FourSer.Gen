using System.Collections.Immutable;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionImmutableArray;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public ImmutableArray<IItem> Items { get; set; } = ImmutableArray<IItem>.Empty;
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
