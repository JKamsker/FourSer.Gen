using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHARLIST_REQ)]
    public partial class CsCharListReq
    {
        /// <summary>
        /// Original name: pUser->m_bGroupID. The ID of the group for which to retrieve the character list.
        /// </summary>
        public byte GroupID;
    }
}
