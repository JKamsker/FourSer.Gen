using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CharacterInfo
    {
        /// <summary>
        /// Original name: m_dwCharID. The unique ID of the character.
        /// </summary>
        public uint CharID;

        /// <summary>
        /// Original name: m_strName. The name of the character.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Original name: m_bStartAct. Starting action or state.
        /// </summary>
        public byte StartAct;

        /// <summary>
        /// Original name: m_bSlot. The character slot index.
        /// </summary>
        public byte Slot;

        /// <summary>
        /// Original name: m_bLevel. The character's level.
        /// </summary>
        public byte Level;

        /// <summary>
        /// Original name: m_bClass. The character's class ID.
        /// </summary>
        public byte ClassId;

        /// <summary>
        /// Original name: m_bRace. The character's race ID.
        /// </summary>
        public byte Race;

        /// <summary>
        /// Original name: m_bCountry. The character's country ID.
        /// </summary>
        public byte Country;

        /// <summary>
        /// Original name: m_bSex. The character's sex.
        /// </summary>
        public byte Sex;

        /// <summary>
        /// Original name: m_bHair. The character's hair style ID.
        /// </summary>
        public byte Hair;

        /// <summary>
        /// Original name: m_bFace. The character's face style ID.
        /// </summary>
        public byte Face;

        /// <summary>
        /// Original name: m_bBody. The character's body type ID.
        /// </summary>
        public byte Body;

        /// <summary>
        /// Original name: m_bPants. The character's pants style ID.
        /// </summary>
        public byte Pants;

        /// <summary>
        /// Original name: m_bHand. The character's hand/glove style ID.
        /// </summary>
        public byte Hand;

        /// <summary>
        /// Original name: m_bFoot. The character's foot/boot style ID.
        /// </summary>
        public byte Foot;

        /// <summary>
        /// Original name: m_dwRegion. The ID of the region the character is in.
        /// </summary>
        public uint Region;

        /// <summary>
        /// Original name: m_dwFame. The character's fame points.
        /// </summary>
        public uint Fame;

        /// <summary>
        /// Original name: m_dwFameColor. The color associated with the character's fame.
        /// </summary>
        public uint FameColor;

        /// <summary>
        /// Original name: m_vTItem. A list of items equipped by the character.
        /// </summary>
        [SerializeCollection(CountType = typeof(byte))]
        public List<ItemInfo> Items = new();
    }
}
