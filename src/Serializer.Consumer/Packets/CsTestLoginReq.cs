using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTLOGIN_REQ)]
    public partial class CsTestLoginReq
    {
    }
}
