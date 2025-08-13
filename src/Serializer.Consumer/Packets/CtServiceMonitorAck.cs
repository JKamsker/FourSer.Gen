using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CtServiceMonitorAck
    {
        public uint DwTick;
    }
}
