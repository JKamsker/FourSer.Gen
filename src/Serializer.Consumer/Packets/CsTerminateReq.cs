using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TERMINATE_REQ)]
    public partial class CsTerminateReq
    {
    }
}
