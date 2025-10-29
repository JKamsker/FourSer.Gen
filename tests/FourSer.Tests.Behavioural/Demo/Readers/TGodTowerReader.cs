using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TGodTower.tcd")]
    [GenerateSerializer]
    public partial class TGodTowerCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TGodTowerRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TGodTowerRecord
    {
        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public uint Id { get; set; }
        public float Rotation { get; set; }
        public uint ObjectId { get; set; }
        public uint IconId { get; set; }
        public uint AttackSfxId { get; set; }
        public uint DefenseSfxId { get; set; }
        public uint NormalSfxId { get; set; }
    }
}
