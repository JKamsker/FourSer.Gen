using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionIEnumerable;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(ItemTypeId))]
    [PolymorphicOption(nameof(ItemTypeId.Sword), typeof(Sword))]
    [PolymorphicOption(nameof(ItemTypeId.Shield), typeof(Shield))]
    [PolymorphicOption(nameof(ItemTypeId.Potion), typeof(Potion))]
    public IEnumerable<Item> Items { get; set; }
}

public enum ItemTypeId : byte
{
    Sword = 10,
    Shield = 20,
    Potion = 30
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