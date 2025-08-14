using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_UNIQUE_HWID_REQ)]
    public partial class CsUniqueHwidReq
    {
    }
}
