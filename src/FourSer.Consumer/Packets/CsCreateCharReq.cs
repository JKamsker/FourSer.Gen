using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CREATECHAR_REQ)]
    public partial class CsCreateCharReq
    {
        /// <summary>
        /// Original name: bGroupID. The ID of the group to create the character in.
        /// </summary>
        public byte GroupID;

        /// <summary>
        /// Original name: strNAME. The name of the character to create.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Original name: bSlotID. The slot for the new character.
        /// </summary>
        public byte SlotID;

        /// <summary>
        /// Original name: bClass. The class of the new character.
        /// </summary>
        public byte ClassId;

        /// <summary>
        /// Original name: bRace. The race of the new character.
        /// </summary>
        public byte Race;

        /// <summary>
        /// Original name: bCountry. The country of the new character.
        /// </summary>
        public byte Country;

        /// <summary>
        /// Original name: bSex. The sex of the new character.
        /// </summary>
        public byte Sex;

        /// <summary>
        /// Original name: bHair. The hair style of the new character.
        /// </summary>
        public byte Hair;

        /// <summary>
        /// Original name: bFace. The face style of the new character.
        /// </summary>
        public byte Face;

        /// <summary>
        /// Original name: bBody. The body type of the new character.
        /// </summary>
        public byte Body;

        /// <summary>
        /// Original name: bPants. The pants style of the new character.
        /// </summary>
        public byte Pants;

        /// <summary>
        /// Original name: bHand. The hand style of the new character.
        /// </summary>
        public byte Hand;

        /// <summary>
        /// Original name: bFoot. The foot style of the new character.
        /// </summary>
        public byte Foot;
    }
}
