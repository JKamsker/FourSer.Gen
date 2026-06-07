using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TString.tcd")]
    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TStringCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TStringEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer(SerializerGenerationMethods.Stream)]
    public partial class TStringEntry
    {
        public ushort StringId { get; set; }

        [Serializer(typeof(MfcStringSerializer))]
        public string Value { get; set; } = string.Empty;
    }
}
