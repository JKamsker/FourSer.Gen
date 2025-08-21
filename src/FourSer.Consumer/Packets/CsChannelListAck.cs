using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHANNELLIST_ACK)]
    public partial class CsChannelListAck
    {
        /// <summary>
        /// Original name: bCount. The number of channels in the list.
        /// </summary>
        public byte ChannelCount;

        /// <summary>
        /// Original name: result of GetCheckFilePoint(pUser). A security or check value.
        /// </summary>
        public long CheckFilePoint;

        /// <summary>
        /// A list of available channels.
        /// </summary>
        [SerializeCollection(CountSizeReference = nameof(ChannelCount))]
        public List<ChannelInfo> Channels = new();
    }
}
