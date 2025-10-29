using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSTEP.tcd")]
    [GenerateSerializer]
    public partial class TStepCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TStepEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TStepEntry
    {
        public uint TileId { get; set; }
        public uint SoundFunctionId { get; set; }
    }
}

