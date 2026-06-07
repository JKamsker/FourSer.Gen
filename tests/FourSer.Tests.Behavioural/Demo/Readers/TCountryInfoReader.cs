using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TCountryInfo.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TCountryInfoCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TCountryInfoEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TCountryInfoEntry
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Text { get; set; } = string.Empty;

        public uint Id { get; set; }
    }
}
