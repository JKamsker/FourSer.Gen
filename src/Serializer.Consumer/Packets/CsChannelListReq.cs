using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHANNELLIST_REQ)]
    public partial class CsChannelListReq
    {
        public byte GroupID;
    }
}
