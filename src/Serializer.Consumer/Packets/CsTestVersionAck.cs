using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTVERSION_ACK)]
    public partial class CsTestVersionAck
    {
        public ushort Version;
    }
}
