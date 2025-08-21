using FourSer.Contracts;

namespace FourSer.Consumer.UseCases;

[GenerateSerializer]
public partial struct LoginReqPacket
{
    public ushort wVersion;
    public string strUserID;
    public string strPasswd;
    public long dlCheck;
}
