using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsUniqueHwidAck
    {
        public string Hwid = string.Empty;
    }
}
