using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTLOGIN_REQ)]
    public partial class CsTestLoginReq
    {
    }
}
