using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CtEventMsgReq
    {
        public byte EventID;
        public byte EventMsgType;
        public string Msg = string.Empty;
    }
}
