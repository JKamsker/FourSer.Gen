using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHARLIST_ACK)]
    public partial class CsCharListAck
    {
        /// <summary>
        /// Original name: result of GetCheckFilePoint(pUser). A security or check value.
        /// </summary>
        public long CheckFilePoint;

        /// <summary>
        /// Original name: vCHAR. A list of characters associated with the user account.
        /// </summary>
        [SerializeCollection(CountType = typeof(byte))]
        public List<CharacterInfo> Characters = new();
    }
}
