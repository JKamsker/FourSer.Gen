using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMonthRank.tcd")]
    [GenerateSerializer]
    public partial class TMonthRankCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TFameTitleEntry> Titles { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TFameTitleEntry
    {
        public byte RankId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Title { get; set; } = string.Empty;

        public byte HasSfx { get; set; }
        public byte SfxId { get; set; }
    }
}
