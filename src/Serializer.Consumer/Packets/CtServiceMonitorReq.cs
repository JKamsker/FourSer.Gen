using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CtServiceMonitorReq
    {
        public uint Tick;
        public uint Session;
        public uint User;
        public uint ActiveUser;
    }
}
