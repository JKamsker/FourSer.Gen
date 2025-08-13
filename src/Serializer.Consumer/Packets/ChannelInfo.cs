using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class ChannelInfo
    {
        /// <summary>
        /// Original name: query->m_szNAME. The name of the channel.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Original name: query->m_bChannel. The ID of the channel.
        /// </summary>
        public byte ChannelID;

        /// <summary>
        /// The calculated status of the channel (e.g., Normal, Busy, Full).
        /// </summary>
        public byte Status;
    }
}
