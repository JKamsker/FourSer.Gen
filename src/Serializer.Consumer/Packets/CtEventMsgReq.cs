using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_EVENTMSG_REQ)]
    public partial class CtEventMsgReq
    {
        public byte EventID;
        public byte EventMsgType;
        public string Msg = string.Empty;
    }
}
