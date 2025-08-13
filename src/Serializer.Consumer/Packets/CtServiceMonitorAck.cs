using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEMONITOR_ACK)]
    public partial class CtServiceMonitorAck
    {
        public uint DwTick;
    }
}
