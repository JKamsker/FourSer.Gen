using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

[GenerateSerializer]
public partial class LoginPacket
{
    public byte Result;
    public uint UserID;
    public string Username;
}
