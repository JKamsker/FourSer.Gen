using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_GROUPLIST_REQ)]
    public partial class CsGroupListReq
    {
    }
}
