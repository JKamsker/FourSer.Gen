using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsTestVersionAck
    {
        public ushort Version;
    }
}
