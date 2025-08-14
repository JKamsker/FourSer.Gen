using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTLOGIN_REQ)]
    public partial class CsTestLoginReq
    {
    }
}
