using System.Collections.Generic;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionIReadOnlyList;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public IReadOnlyList<IItem> Items { get; set; } = new List<IItem>();
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
