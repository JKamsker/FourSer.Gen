using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_LOGIN_REQ)]
    public partial class CsLoginReq
    {
        public ushort Version;
        public string UserID = string.Empty;
        public string Passwd = string.Empty;
        public long Check;
    }
}
