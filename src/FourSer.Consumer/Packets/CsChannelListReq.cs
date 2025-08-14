using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHANNELLIST_REQ)]
    public partial class CsChannelListReq
    {
        /// <summary>
        /// Original name: query->m_bGroupID. The ID of the group for which to retrieve the channel list.
        /// </summary>
        public byte GroupID;
    }
}
