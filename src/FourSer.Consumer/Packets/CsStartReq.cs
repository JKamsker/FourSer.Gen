using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_START_REQ)]
    public partial class CsStartReq
    {
        /// <summary>
        /// Original name: bGroupID. The ID of the group the character is in.
        /// </summary>
        public byte GroupID;

        /// <summary>
        /// Original name: bChannel. The ID of the channel to join.
        /// </summary>
        public byte Channel;

        /// <summary>
        /// Original name: dwCharID. The ID of the character to start with.
        /// </summary>
        public uint CharID;
    }
}
