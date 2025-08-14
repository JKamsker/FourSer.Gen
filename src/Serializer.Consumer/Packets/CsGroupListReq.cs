using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_GROUPLIST_REQ)]
    public partial class CsGroupListReq
    {
    }
}
