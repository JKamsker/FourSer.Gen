using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CT_EVENTUPDATE_REQ)]
    public partial class CtEventUpdateReq
    {
        public byte EventID;
        public ushort Value;
    }
}
