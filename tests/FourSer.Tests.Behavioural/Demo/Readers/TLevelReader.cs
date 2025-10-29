using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TLevel.tcd")]
    [GenerateSerializer]
    public partial class TLevelCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TLevelEntry> Levels { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TLevelEntry
    {
        public byte Level { get; set; }
        public uint Cost { get; set; }
        public uint RegisterCost { get; set; }
        public uint SearchCost { get; set; }
    }
}

