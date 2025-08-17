using FourSer.Contracts;
using System;
using System.Collections.Generic;

namespace FourSer.TestUser
{
    [GenerateSerializer]
    public partial class Inventory
    {
        public int TypeId { get; set; }

        [SerializeCollection(TypeIdProperty = nameof(TypeId))]
        [PolymorphicOption((byte)10, typeof(Sword))]
        [PolymorphicOption((byte)20, typeof(Shield))]
        [PolymorphicOption((byte)30, typeof(Potion))]
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
}
