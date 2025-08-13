using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class ChannelInfo
    {
        public string Name = string.Empty;
        public byte ChannelID;
        public byte Status;
    }
}
