using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEDATACLEAR_ACK)]
    public partial class CtServiceDataClearAck
    {
    }
}
