using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_DELCHAR_REQ)]
    public partial class CsDelCharReq
    {
        /// <summary>
        /// Original name: bGroupID. The ID of the group the character belongs to.
        /// </summary>
        public byte GroupID;

        /// <summary>
        /// Original name: strPasswd. The user's password for verification.
        /// </summary>
        public string Passwd = string.Empty;

        /// <summary>
        /// Original name: dwCharID. The ID of the character to delete.
        /// </summary>
        public uint CharID;
    }
}
