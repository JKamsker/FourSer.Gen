using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TRace.tcd")]
    [GenerateSerializer]
    public partial class TRaceCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TRaceEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TRaceEntry
    {
        public byte RaceId { get; set; }
        public byte SexId { get; set; }
        public uint ObjectId { get; set; }
        public float Scale { get; set; }
    }
}

