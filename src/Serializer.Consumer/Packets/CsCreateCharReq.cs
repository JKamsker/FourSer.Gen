using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsCreateCharReq
    {
        public byte GroupID;
        public string Name = string.Empty;
        public byte SlotID;
        public byte ClassId;
        public byte Race;
        public byte Country;
        public byte Sex;
        public byte Hair;
        public byte Face;
        public byte Body;
        public byte Pants;
        public byte Hand;
        public byte Foot;
    }
}
