using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEMONITOR_ACK)]
    public partial class CtServiceMonitorAck
    {
        /// <summary>
        /// Original name: dwTick. A tick value for monitoring.
        /// </summary>
        public uint DwTick;
    }
}
