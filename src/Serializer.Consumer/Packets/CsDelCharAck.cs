using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_DELCHAR_ACK)]
    public partial class CsDelCharAck
    {
        public byte Result;
        public uint CharID;
    }
}
