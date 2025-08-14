using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_DELCHAR_ACK)]
    public partial class CsDelCharAck
    {
        /// <summary>
        /// Original name: bResult. The result of the character deletion request.
        /// </summary>
        public byte Result;

        /// <summary>
        /// Original name: dwCharID. The ID of the character that was requested to be deleted.
        /// </summary>
        public uint CharID;
    }
}
