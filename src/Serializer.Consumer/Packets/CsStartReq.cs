using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_START_REQ)]
    public partial class CsStartReq
    {
        public byte GroupID;
        public byte Channel;
        public uint CharID;
    }
}
