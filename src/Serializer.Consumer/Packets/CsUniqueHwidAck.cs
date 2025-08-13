using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_UNIQUE_HWID_ACK)]
    public partial class CsUniqueHwidAck
    {
        public string Hwid = string.Empty;
    }
}
