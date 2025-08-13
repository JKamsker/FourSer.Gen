using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class GroupInfo
    {
        /// <summary>
        /// Original name: query->m_szNAME. The name of the group/server.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Original name: query->m_bGroupID. The ID of the group.
        /// </summary>
        public byte GroupID;

        /// <summary>
        /// Original name: query->m_bType. The type of the group.
        /// </summary>
        public byte Type;

        /// <summary>
        /// The calculated status of the group (e.g., Normal, Busy, Full).
        /// </summary>
        public byte Status;

        /// <summary>
        /// Original name: query->m_bCount. The number of servers in the group.
        /// </summary>
        public byte ServerCount;
    }
}
