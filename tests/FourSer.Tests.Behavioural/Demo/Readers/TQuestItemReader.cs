using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TQuestItem.tcd")]
    [GenerateSerializer]
    public partial class TQuestItemCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TQuestItemEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TQuestItemEntry
    {
        public uint RewardId { get; set; }
        public ushort ItemId { get; set; }
        public byte Grade { get; set; }
        public byte GradeLevel { get; set; }
        public uint DurabilityMax { get; set; }
        public uint DurabilityCurrent { get; set; }
        public byte RefineCurrent { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.MagicOptionCount)]
        public byte[] MagicKinds { get; set; } = new byte[ReaderConstants.MagicOptionCount];

        [SerializeCollection(CountSize = ReaderConstants.MagicOptionCount)]
        public byte[] MagicValues { get; set; } = new byte[ReaderConstants.MagicOptionCount];
    }
}

