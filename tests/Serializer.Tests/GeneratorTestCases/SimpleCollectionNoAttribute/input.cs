namespace Serializer.Tests.GeneratorTestCases.SimpleCollectionNoAttribute;

[GenerateSerializer]
public partial class MyPacket
{
    // Should act like ``[SerializeCollection]`` was present without arguments
    public List<byte> Data { get; set; } = new();
}