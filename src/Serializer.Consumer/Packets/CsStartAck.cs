using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsStartAck
    {
        public byte Result;
        public uint MapIP;
        public ushort Port;
        public byte ServerID;
    }
}
