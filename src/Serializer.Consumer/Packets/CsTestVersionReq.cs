using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_TESTVERSION_REQ)]
    public partial class CsTestVersionReq
    {
    }
}
