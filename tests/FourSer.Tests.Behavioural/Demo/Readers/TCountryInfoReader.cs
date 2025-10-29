using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TCountryInfo.tcd")]
    [GenerateSerializer]
    public partial class TCountryInfoCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TCountryInfoEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TCountryInfoEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Text { get; set; } = string.Empty;

        public uint Id { get; set; }
    }
}
