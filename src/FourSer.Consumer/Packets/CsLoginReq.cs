using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_LOGIN_REQ)]
    public partial class CsLoginReq
    {
        /// <summary>
        /// Original name: wVersion. The client's version.
        /// </summary>
        public ushort Version;

        /// <summary>
        /// Original name: pUser->m_strUserID. The user's ID or username.
        /// </summary>
        public string UserID = string.Empty;

        /// <summary>
        /// Original name: pUser->m_strPasswd. The user's password.
        /// </summary>
        public string Passwd = string.Empty;

        /// <summary>
        /// Original name: dlCheck. A security check value.
        /// </summary>
        public long Check;
    }
}
