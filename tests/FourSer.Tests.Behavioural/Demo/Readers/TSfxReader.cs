using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSFX.tcd")]
    [GenerateSerializer]
    public partial class TSfxCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSfxEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TSfxEntry
    {
        public uint EffectId { get; set; }
        public uint ResourceId { get; set; }
        public uint PositionSetId { get; set; }
    }
}

