using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_UNIQUE_HWID_ACK)]
    public partial class CsUniqueHwidAck
    {
        /// <summary>
        /// Original name: strHWID. The hardware ID of the client.
        /// </summary>
        public string Hwid = string.Empty;
    }
}
