using FourSer.Contracts;

namespace FourSer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_UNIQUE_HWID_REQ)]
    public partial class CsUniqueHwidReq
    {
    }
}
