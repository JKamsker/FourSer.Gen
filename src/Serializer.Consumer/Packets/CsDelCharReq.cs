using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsDelCharReq
    {
        public byte GroupID;
        public string Passwd = string.Empty;
        public uint CharID;
    }
}
