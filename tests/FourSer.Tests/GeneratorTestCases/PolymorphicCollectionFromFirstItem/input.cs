using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionFromFirstItem;

[GenerateSerializer]
public partial class Inventory
{
    // Note: TypeId is here but not used by the collection serialization itself.
    // It's just a property on the class.
    public int TypeId { get; set; }

    [SerializeCollection(CountType = typeof(int))]
    [PolymorphicOption(10, typeof(Sword))]
    [PolymorphicOption(20, typeof(Shield))]
    [PolymorphicOption(30, typeof(Potion))]
    public List<Item> Items { get; set; }
}

[GenerateSerializer]
public partial class Item
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class Sword : Item
{
    public int Damage { get; set; }
}

[GenerateSerializer]
public partial class Shield : Item
{
    public int Defense { get; set; }
}

[GenerateSerializer]
public partial class Potion : Item
{
    public int HealAmount { get; set; }
}
