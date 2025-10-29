using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemAttr.tcd")]
    [GenerateSerializer]
    public partial class TItemAttrCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemAttrRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TItemAttrRecord
    {
        public ushort Id { get; set; }
        public byte Grade { get; set; }
        public ushort MinAttack { get; set; }
        public ushort MaxAttack { get; set; }
        public ushort Defense { get; set; }
        public ushort MinMagicAttack { get; set; }
        public ushort MaxMagicAttack { get; set; }
        public ushort MagicDefense { get; set; }
        public byte BlockProbability { get; set; }
        public byte SpeedIncrease { get; set; }
    }
}
