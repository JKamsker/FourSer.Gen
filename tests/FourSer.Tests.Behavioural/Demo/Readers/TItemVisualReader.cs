using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TItemVisual.tcd")]
    [GenerateSerializer]
    public partial class TItemVisualCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItemVisualRecord> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TItemVisualRecord
    {
        public ushort Id { get; set; }
        public uint InventoryVisualId { get; set; }
        public uint ObjectId { get; set; }
        public uint ClkId { get; set; }
        public uint CliId { get; set; }
        public uint MeshNormal { get; set; }
        public uint MeshBattle { get; set; }
        public uint PivotNormal { get; set; }
        public uint PivotBattle { get; set; }
        public ushort IconId { get; set; }
        public byte HideSlotMask { get; set; }
        public byte HidePartMask { get; set; }
        public byte HideRaceMask { get; set; }
        public uint SlashColor { get; set; }
        public uint SlashTextureId { get; set; }
        public float SlashLength { get; set; }

        [SerializeCollection(CountSize = 2)]
        public uint[] EffectFunctionIds { get; set; } = new uint[2];

        public uint CostumeHideMask { get; set; }
    }
}
