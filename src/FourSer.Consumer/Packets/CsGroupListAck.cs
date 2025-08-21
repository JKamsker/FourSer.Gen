using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_GROUPLIST_ACK)]
    public partial class CsGroupListAck
    {
        /// <summary>
        /// Original name: bCount. The number of groups in the list.
        /// </summary>
        public byte GroupCount;

        /// <summary>
        /// Original name: result of GetCheckFilePoint(pUser). A security or check value.
        /// </summary>
        public long CheckFilePoint;

        /// <summary>
        /// A list of available server groups.
        /// </summary>
        [SerializeCollection(CountSizeReference = nameof(GroupCount))]
        public List<GroupInfo> Groups = new();
    }
}
