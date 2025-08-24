using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTVERSION_REQ)]
    public partial class CsTestVersionReq
    {
    }
}
