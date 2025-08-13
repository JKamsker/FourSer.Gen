using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class GroupInfo
    {
        public string Name = string.Empty;
        public byte GroupID;
        public byte Type;
        public byte Status;
        public byte ServerCount;
    }
}
