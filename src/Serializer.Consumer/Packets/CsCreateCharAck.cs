using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CREATECHAR_ACK)]
    public partial class CsCreateCharAck
    {
        /// <summary>
        /// Original name: bResult. The result of the character creation request.
        /// </summary>
        public byte Result;

        /// <summary>
        /// Original name: dwID. The new character's ID.
        /// </summary>
        public uint ID;

        /// <summary>
        /// Original name: strNAME. The name of the new character.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Original name: bSlotID. The slot ID for the new character.
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

        /// <summary>
        /// Original name: bCreateCnt. The number of characters created.
        /// </summary>
        public byte CreateCnt;
    }
}
