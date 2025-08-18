using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionIEnumerable;

[GenerateSerializer]
public partial class Inventory
{
    public int ItemTypeId { get; set; }

    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = nameof(ItemTypeId))]
    [PolymorphicOption(0, typeof(Sword))]
    [PolymorphicOption(1, typeof(Shield))]
    [PolymorphicOption(2, typeof(Potion))]
    public List<Item> Items { get; set; }
}

[GenerateSerializer]
public partial class Item
{
}


[GenerateSerializer]
public partial class Sword : Item
{
}

[GenerateSerializer]
public partial class Shield : Item
{
}

[GenerateSerializer]
public partial class Potion : Item
{
}