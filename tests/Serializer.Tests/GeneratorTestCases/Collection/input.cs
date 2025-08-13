namespace TestNamespace;

[GenerateSerializer]
public partial class CollectionPacket
{
    [SerializeCollection]
    public List<int> Numbers { get; set; }
}
