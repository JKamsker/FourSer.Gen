using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsHotsendReq
    {
        public long Value;
        public byte All;
    }
}
