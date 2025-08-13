using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_DELCHAR_REQ)]
    public partial class CsDelCharReq
    {
        public byte GroupID;
        public string Passwd = string.Empty;
        public uint CharID;
    }
}
