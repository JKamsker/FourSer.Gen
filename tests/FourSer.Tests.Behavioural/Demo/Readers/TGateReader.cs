using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TGate.tcd")]
    [GenerateSerializer]
    public partial class TGateCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TGateEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TGateEntry
    {
        private const int EquipmentSlotCount = 19;

        public uint Id { get; set; }
        public uint SwitchId { get; set; }
        public ushort MapId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float Direction { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }
        public uint ObjectId { get; set; }

        [SerializeCollection(CountSize = EquipmentSlotCount)]
        public ushort[] RequiredItemIds { get; set; } = new ushort[EquipmentSlotCount];

        public byte CloseId { get; set; }
        public byte CloseActionId { get; set; }
        public byte OpenId { get; set; }
        public byte OpenActionId { get; set; }
        public byte DeleteOnOpen { get; set; }
        public byte DeleteOnClose { get; set; }
        public byte HouseMesh { get; set; }
        public uint HouseId { get; set; }
        public uint OpenSfxId { get; set; }
        public uint CloseSfxId { get; set; }
        public byte IsStair { get; set; }
    }
}
