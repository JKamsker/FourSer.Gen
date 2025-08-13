using System.Collections.Generic;
using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHANNELLIST_ACK)]
    public partial class CsChannelListAck
    {
        public byte ChannelCount;
        public long CheckFilePoint;

        [SerializeCollection(CountSizeReference = nameof(ChannelCount))]
        public List<ChannelInfo> Channels = new();
    }
}
