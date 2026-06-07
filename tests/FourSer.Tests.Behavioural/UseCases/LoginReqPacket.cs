using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer(SerializerGenerationMethods.Stream)]
public partial struct LoginReqPacket
{
    public ushort wVersion;
    public string strUserID;
    public string strPasswd;
    public long dlCheck;
}