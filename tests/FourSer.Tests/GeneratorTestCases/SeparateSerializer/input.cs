using FourSer.Contracts;

namespace Testing;

[GenerateSerializer(Mode = GenerationMode.Separate)]
public partial class MySeparatePacket
{
    public int Id { get; set; }
    public string Name { get; set; }
}
