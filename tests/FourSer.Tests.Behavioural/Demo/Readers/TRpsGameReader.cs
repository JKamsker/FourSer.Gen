using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TRpsGame.tcd")]
    [GenerateSerializer]
    public partial class TRpsGameCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TRpsGameEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TRpsGameEntry
    {
        public byte GameType { get; set; }
        public byte WinCondition { get; set; }
        public ushort RequiredItemId { get; set; }
        public ushort RewardItem1Id { get; set; }
        public byte RewardItem1Count { get; set; }
        public ushort RewardItem2Id { get; set; }
        public byte RewardItem2Count { get; set; }
        public uint RewardMoney { get; set; }
    }
}

