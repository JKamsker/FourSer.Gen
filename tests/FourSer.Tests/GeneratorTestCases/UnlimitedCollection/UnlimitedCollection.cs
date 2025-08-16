using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.UnlimitedCollection;

[GenerateSerializer]
public partial class UnlimitedItem
{
    public int Value { get; set; }
}

[GenerateSerializer]
public partial class UnlimitedCollectionPacket
{
    [SerializeCollection(Unlimited = true)]
    public List<UnlimitedItem> Items { get; set; }
}
