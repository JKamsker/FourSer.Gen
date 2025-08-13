using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CtEventUpdateReq
    {
        public byte EventID;
        public ushort Value;
    }
}
