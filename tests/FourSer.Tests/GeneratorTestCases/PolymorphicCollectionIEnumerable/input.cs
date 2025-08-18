namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionIEnumerable;

[GenerateSerializer]
public partial class Inventory
{
    public int TypeId { get; set; }
    public int Count { get; set; }

    [SerializeCollection
    (
        TypeIdProperty = "TypeId",
        CountSizeReference = nameof(Count)
    )]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public IEnumerable<Item> Items { get; set; } = new List<Item>();
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