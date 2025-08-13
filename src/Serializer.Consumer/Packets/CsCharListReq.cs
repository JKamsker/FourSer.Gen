using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHARLIST_REQ)]
    public partial class CsCharListReq
    {
        public byte GroupID;
    }
}
