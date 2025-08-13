using System.Collections.Generic;
using Serializer.Contracts;

namespace Serializer.Consumer.Packets
{
    [GenerateSerializer]
    [OpCode(OpCode.CS_GROUPLIST_ACK)]
    public partial class CsGroupListAck
    {
        public byte GroupCount;
        public long CheckFilePoint;

        [SerializeCollection(CountSizeReference = nameof(GroupCount))]
        public List<GroupInfo> Groups = new();
    }
}
