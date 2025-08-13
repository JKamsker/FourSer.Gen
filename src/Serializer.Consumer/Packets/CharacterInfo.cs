using System.Collections.Generic;
using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CharacterInfo
    {
        public uint CharID;
        public string Name = string.Empty;
        public byte StartAct;
        public byte Slot;
        public byte Level;
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
        public uint Region;
        public uint Fame;
        public uint FameColor;

        [SerializeCollection(CountType = typeof(byte))]
        public List<ItemInfo> Items = new();
    }
}
