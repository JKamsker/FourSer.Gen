using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

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

