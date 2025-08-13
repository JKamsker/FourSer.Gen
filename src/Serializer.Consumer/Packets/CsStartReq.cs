using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsStartReq
    {
        public byte GroupID;
        public byte Channel;
        public uint CharID;
    }
}
