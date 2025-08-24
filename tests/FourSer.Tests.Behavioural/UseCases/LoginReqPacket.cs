using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial struct LoginReqPacket
{
    public ushort wVersion;
    public string strUserID;
    public string strPasswd;
    public long dlCheck;
}