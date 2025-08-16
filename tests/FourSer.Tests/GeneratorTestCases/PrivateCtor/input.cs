using FourSer.Contracts;

namespace Test;

[GenerateSerializer]
public partial class PrivateCtorPacket
{
    private PrivateCtorPacket() { }
    public int Value { get; set; }
}
