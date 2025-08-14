namespace Serializer.Tests.GeneratorTestCases.SimplePacket;

[GenerateSerializer]
public partial class LoginPacket
{
    public byte Result;
    public uint UserID;
    public string Username;
}
