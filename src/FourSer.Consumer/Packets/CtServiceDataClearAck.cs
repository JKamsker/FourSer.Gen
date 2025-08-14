using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEDATACLEAR_ACK)]
    public partial class CtServiceDataClearAck
    {
    }
}
