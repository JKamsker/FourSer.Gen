using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TFog.tcd")]
    [GenerateSerializer]
    public partial class TFogCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TFogEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFogEntry
    {
        public uint FogId { get; set; }
        public byte ScopeFlag { get; set; }
        public byte Type { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public float Radius { get; set; }
        public float Range { get; set; }
        public float PositionX { get; set; }
        public float PositionZ { get; set; }
        public float Density { get; set; }
        public float Start { get; set; }
        public float End { get; set; }
    }
}
