using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_HOTSEND_REQ)]
    public partial class CsHotsendReq
    {
        public long Value;
        public byte All;
    }
}
