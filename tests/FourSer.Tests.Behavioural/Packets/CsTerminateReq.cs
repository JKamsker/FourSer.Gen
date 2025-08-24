using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TERMINATE_REQ)]
    public partial class CsTerminateReq
    {
    }
}
