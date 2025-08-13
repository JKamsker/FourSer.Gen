using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsDelCharAck
    {
        public byte Result;
        public uint CharID;
    }
}
