using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TAction.tcd")]
    [GenerateSerializer]
    public partial class TActionCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TActionRecord> Actions { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TActionRecord
    {
        public uint Key { get; set; }
        public uint ActionId { get; set; }
        public byte EquipMode { get; set; }
        public uint SlashEffectId { get; set; }
    }
}
