using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class Item
{
    public int Id { get; set; }
}
