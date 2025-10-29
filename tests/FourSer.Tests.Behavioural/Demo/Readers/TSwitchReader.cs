using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSwitch.tcd")]
    [GenerateSerializer]
    public partial class TSwitchCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSwitchEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TSwitchEntry
    {
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

        [SerializeCollection(CountSize = ReaderConstants.EquipmentSlotCount)]
        public ushort[] RequiredItemIds { get; set; } = new ushort[ReaderConstants.EquipmentSlotCount];

        public byte CloseStateId { get; set; }
        public byte OpenStateId { get; set; }
        public byte LockOnOpenFlag { get; set; }
        public byte LockOnCloseFlag { get; set; }
        public byte HouseMeshFlag { get; set; }
        public uint HouseId { get; set; }
        public uint SoundId { get; set; }
    }
}

