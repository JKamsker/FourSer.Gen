using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
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
