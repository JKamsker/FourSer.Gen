using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_HOTSEND_ACK)]
    public partial class CsHotsendAck
    {
    }
}
