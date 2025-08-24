using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_UNIQUE_HWID_REQ)]
    public partial class CsUniqueHwidReq
    {
    }
}
