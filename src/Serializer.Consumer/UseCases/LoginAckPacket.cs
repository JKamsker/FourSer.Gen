using Serializer.Contracts;

namespace Serializer.Consumer.UseCases;

[GenerateSerializer]
public partial class LoginAckPacket
{
    public byte bResult;
    public uint dwUserID;
    public uint dwKickID;
    public uint dwKEY;
    public uint Address;
    public ushort Port;
    public byte bCreateCardCnt;
    public byte bInPcRoom;
    public uint dwPremiumPcRoom;
    public long dCurrentTime;
    public long dKey;
}