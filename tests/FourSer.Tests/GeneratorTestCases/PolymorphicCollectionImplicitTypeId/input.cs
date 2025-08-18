using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionImplicitTypeId;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte))]
    [PolymorphicOption(10, typeof(Sword))]
    [PolymorphicOption(20, typeof(Shield))]
    [PolymorphicOption(30, typeof(Potion))]
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
