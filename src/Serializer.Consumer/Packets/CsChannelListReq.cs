using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsChannelListReq
    {
        public byte GroupID;
    }
}
