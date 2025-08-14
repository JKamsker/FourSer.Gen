using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TERMINATE_REQ)]
    public partial class CsTerminateReq
    {
    }
}
