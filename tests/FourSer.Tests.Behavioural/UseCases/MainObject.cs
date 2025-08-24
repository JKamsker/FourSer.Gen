using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class MainObject
{
    [SerializeCollection(CountSize = 10)]
    public List<Item> Items { get; set; }
}
