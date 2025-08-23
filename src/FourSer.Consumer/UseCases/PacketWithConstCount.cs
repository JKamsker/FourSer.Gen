using FourSer.Contracts;

namespace FourSer.Consumer.UseCases.ConstCount;

[GenerateSerializer]
public partial class Item
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class MainObject
{
    [SerializeCollection(CountSize = 10)]
    public List<Item> Items { get; set; }
}
