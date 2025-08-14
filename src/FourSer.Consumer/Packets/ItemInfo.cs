using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class ItemInfo
    {
        /// <summary>
        /// Original name: m_bItemID. The base ID of the item.
        /// </summary>
        public byte BItemID;

        /// <summary>
        /// Original name: m_wItemID. The specific ID of the item.
        /// </summary>
        public ushort WItemID;

        /// <summary>
        /// Original name: m_bLevel. The level of the item.
        /// </summary>
        public byte Level;

        /// <summary>
        /// Original name: m_bGradeEffect. The grade effect of the item.
        /// </summary>
        public byte GradeEffect;

        /// <summary>
        /// Original name: m_wColor. The color of the item.
        /// </summary>
        public ushort Color;

        /// <summary>
        /// Original name: m_bRegGuild. A flag indicating if the item is registered to a guild.
        /// </summary>
        public byte RegGuild;
    }
}
