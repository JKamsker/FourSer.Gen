using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsLoginReqJapan
    {
        public ushort Version;
        public string UserID = string.Empty;
        public string Passwd = string.Empty;
        public long Check;
        public byte Channeling;
    }
}
