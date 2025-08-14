using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTVERSION_ACK)]
    public partial class CsTestVersionAck
    {
        /// <summary>
        /// Original name: wVersion. The server's version.
        /// </summary>
        public ushort Version;
    }
}
