using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_START_ACK)]
    public partial class CsStartAck
    {
        /// <summary>
        /// Original name: bResult. The result of the start request.
        /// </summary>
        public byte Result;

        /// <summary>
        /// Original name: dwMapIP. The IP address of the map server.
        /// </summary>
        public uint MapIP;

        /// <summary>
        /// Original name: wPort. The port of the map server.
        /// </summary>
        public ushort Port;

        /// <summary>
        /// Original name: bServerID. The ID of the server to connect to.
        /// </summary>
        public byte ServerID;
    }
}
