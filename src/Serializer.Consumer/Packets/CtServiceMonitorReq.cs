using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_SERVICEMONITOR_REQ)]
    public partial class CtServiceMonitorReq
    {
        public uint Tick;
        public uint Session;
        public uint User;
        public uint ActiveUser;
    }
}
