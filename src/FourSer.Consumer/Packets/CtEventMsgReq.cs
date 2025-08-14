using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_EVENTMSG_REQ)]
    public partial class CtEventMsgReq
    {
        /// <summary>
        /// Original name: bEventID. The ID of the event.
        /// </summary>
        public byte EventID;

        /// <summary>
        /// Original name: bEventMsgType. The type of the event message.
        /// </summary>
        public byte EventMsgType;

        /// <summary>
        /// Original name: strMsg. The event message content.
        /// </summary>
        public string Msg = string.Empty;
    }
}
