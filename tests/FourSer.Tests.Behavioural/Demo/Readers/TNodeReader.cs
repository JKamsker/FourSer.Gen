using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TNODE.tcd")]
    [GenerateSerializer]
    public partial class TNodeCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TNodeRecord> Nodes { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TNodeRecord
    {
        public uint MapId { get; set; }
        public uint NodeId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
    }
}

