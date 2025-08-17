namespace FourSer.Tests.GeneratorTestCases.Collection;

[GenerateSerializer]
public partial class CollectionPacket
{
    [SerializeCollection]
    public List<int> Numbers { get; set; }
}
