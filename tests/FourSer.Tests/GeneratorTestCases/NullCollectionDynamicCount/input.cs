using FourSer.Contracts;
using System.Collections.Generic;

namespace FourSer.Tests.GeneratorTestCases.NullCollectionDynamicCount;

[GenerateSerializer]
public partial class Item
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class MainObject
{
    [SerializeCollection(CountType = typeof(byte))]
    public List<Item> Items { get; set; }
}
