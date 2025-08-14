using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTVERSION_REQ)]
    public partial class CsTestVersionReq
    {
    }
}
