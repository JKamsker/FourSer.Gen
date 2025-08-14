using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_LOGIN_ACK)]
    public partial class CsLoginAck
    {
        /// <summary>
        /// Original name: bResult. The result of the login attempt.
        /// </summary>
        public byte Result;

        /// <summary>
        /// Original name: dwUserID. The user's account ID.
        /// </summary>
        public uint UserID;

        /// <summary>
        /// Original name: dwCharID. The ID of the character if one is already selected.
        /// </summary>
        public uint CharID;

        /// <summary>
        /// Original name: dwKEY. A session key.
        /// </summary>
        public uint KEY;

        /// <summary>
        /// Original name: dwIPAddr. The IP address of the map server to connect to.
        /// </summary>
        public uint IPAddr;

        /// <summary>
        /// Original name: wPort. The port of the map server to connect to.
        /// </summary>
        public ushort Port;

        /// <summary>
        /// Original name: bCreateCnt. The number of characters created.
        /// </summary>
        public byte CreateCnt;

        /// <summary>
        /// Original name: bInPcBang. A flag indicating if the user is in a PC bang.
        /// </summary>
        public byte InPcBang;

        /// <summary>
        /// Original name: dwPremium. The type of premium service the user has.
        /// </summary>
        public uint Premium;

        /// <summary>
        /// Original name: dCurTime. The current server time.
        /// </summary>
        public long CurTime;

        /// <summary>
        /// Original name: m_dlCheckKey. A security key for client-server checks.
        /// </summary>
        public long CheckKey;
    }
}
