using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial class InventoryWithCountSizeRef
{
    // public int Count { get; set; }
    // public byte TypeId { get; set; }
    
    public byte TypeId { get; set; }
    public int Count { get; set; }

    [SerializeCollection
    (
        TypeIdProperty = "TypeId",
        CountSizeReference = nameof(Count)
    )]
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
