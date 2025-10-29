using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TInfo.tcd")]
    [GenerateSerializer]
    public partial class TInfoCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TInfoRecord> Records { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TInfoRecord
    {
        public uint Id { get; set; }
        public byte TextCount { get; set; }

        [SerializeCollection(CountSizeReference = nameof(TextCount))]
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public List<string> Texts { get; set; } = new();
    }
}
