using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsLoginAck
    {
        public byte Result;
        public uint UserID;
        public uint CharID;
        public uint KEY;
        public uint IPAddr;
        public ushort Port;
        public byte CreateCnt;
        public byte InPcBang;
        public uint Premium;
        public long CurTime;
        public long CheckKey;
    }
}
