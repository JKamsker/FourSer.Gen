using FourSer.Contracts;
using System.Collections.Generic;

namespace FourSer.Tests.GeneratorTestCases.NullCollectionStaticCount;

[GenerateSerializer]
public partial class Item
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class MainObject
{
    [SerializeCollection]
    public List<Item> Items { get; set; }
}
