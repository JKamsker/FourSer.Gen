using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_HOTSEND_ACK)]
    public partial class CsHotsendAck
    {
    }
}
