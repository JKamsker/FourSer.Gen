using System.Collections.Generic;
using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    public partial class CsCharListAck
    {
        public long CheckFilePoint;

        [SerializeCollection(CountType = typeof(byte))]
        public List<CharacterInfo> Characters = new();
    }
}
