using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEDATACLEAR_ACK)]
    public partial class CtServiceDataClearAck
    {
    }
}
