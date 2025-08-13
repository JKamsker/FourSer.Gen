using System.Collections.Generic;
using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_CHARLIST_ACK)]
    public partial class CsCharListAck
    {
        public long CheckFilePoint;

        [SerializeCollection(CountType = typeof(byte))]
        public List<CharacterInfo> Characters = new();
    }
}
