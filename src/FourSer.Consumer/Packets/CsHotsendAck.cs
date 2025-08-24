using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_HOTSEND_ACK)]
    public partial class CsHotsendAck
    {
    }
}
