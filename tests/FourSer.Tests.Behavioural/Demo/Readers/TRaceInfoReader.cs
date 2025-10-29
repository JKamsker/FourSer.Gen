using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TRaceInfo.tcd")]
    [GenerateSerializer]
    public partial class TRaceInfoCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TRaceInfoEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TRaceInfoEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Description { get; set; } = string.Empty;

        public uint InfoId { get; set; }
    }
}
