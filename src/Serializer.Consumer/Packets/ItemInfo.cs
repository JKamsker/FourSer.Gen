using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class ItemInfo
    {
        public byte BItemID;
        public ushort WItemID;
        public byte Level;
        public byte GradeEffect;
        public ushort Color;
        public byte RegGuild;
    }
}
