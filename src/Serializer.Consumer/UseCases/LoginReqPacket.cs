using Serializer.Contracts;

namespace Serializer.Consumer;

[GenerateSerializer]
public partial struct LoginReqPacket
{
    public ushort wVersion;
    public string strUserID;
    public string strPasswd;
    public long dlCheck;
}