using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TNPCGlobal.tcd")]
    [TcdResource("TNPC00000000.tcd")]
    [TcdResource("TNPC00000302.tcd")]
    [TcdResource("TNPC00000303.tcd")]
    [TcdResource("TNPC00000304.tcd")]
    [TcdResource("TNPC00000305.tcd")]
    [TcdResource("TNPC00000401.tcd")]
    [TcdResource("TNPC00000402.tcd")]
    [TcdResource("TNPC00000403.tcd")]
    [TcdResource("TNPC00000404.tcd")]
    [TcdResource("TNPC00000405.tcd")]
    [TcdResource("TNPC00000406.tcd")]
    [TcdResource("TNPC00000502.tcd")]
    [TcdResource("TNPC00000503.tcd")]
    [TcdResource("TNPC00000504.tcd")]
    [TcdResource("TNPC00000505.tcd")]
    [TcdResource("TNPC00000506.tcd")]
    [TcdResource("TNPC00000602.tcd")]
    [TcdResource("TNPC00000603.tcd")]
    [TcdResource("TNPC00000604.tcd")]
    [TcdResource("TNPC00000606.tcd")]
    [TcdResource("TNPC00030000.tcd")]
    [TcdResource("TNPC00040000.tcd")]
    [TcdResource("TNPC00060000.tcd")]
    [TcdResource("TNPC00070000.tcd")]
    [TcdResource("TNPC00080000.tcd")]
    [TcdResource("TNPC00090000.tcd")]
    [TcdResource("TNPC000a0000.tcd")]
    [TcdResource("TNPC000b0000.tcd")]
    [TcdResource("TNPC000c0000.tcd")]
    [TcdResource("TNPC000d0000.tcd")]
    [TcdResource("TNPC000e0000.tcd")]
    [TcdResource("TNPC000f0000.tcd")]
    [TcdResource("TNPC00100000.tcd")]
    [TcdResource("TNPC00110000.tcd")]
    [TcdResource("TNPC01f40000.tcd")]
    [TcdResource("TNPC01f50000.tcd")]
    [TcdResource("TNPC01f60000.tcd")]
    [TcdResource("TNPC01f70000.tcd")]
    [TcdResource("TNPC01f80000.tcd")]
    [TcdResource("TNPC01f90000.tcd")]
    [TcdResource("TNPC01fa0000.tcd")]
    [TcdResource("TNPC01fb0000.tcd")]
    [TcdResource("TNPC01fc0000.tcd")]
    [TcdResource("TNPC01fd0000.tcd")]
    [TcdResource("TNPC01fe0000.tcd")]
    [TcdResource("TNPC01ff0000.tcd")]
    [TcdResource("TNPC02000000.tcd")]
    [TcdResource("TNPC02010000.tcd")]
    [TcdResource("TNPC02020000.tcd")]
    [TcdResource("TNPC02030000.tcd")]
    [TcdResource("TNPC02040000.tcd")]
    [TcdResource("TNPC02050000.tcd")]
    [TcdResource("TNPC02060000.tcd")]
    [TcdResource("TNPC02070000.tcd")]
    [TcdResource("TNPC02080000.tcd")]
    [TcdResource("TNPC02090000.tcd")]
    [TcdResource("TNPC020a0000.tcd")]
    [TcdResource("TNPC020b0000.tcd")]
    [TcdResource("TNPC020c0000.tcd")]
    [TcdResource("TNPC020d0000.tcd")]
    [TcdResource("TNPC020e0000.tcd")]
    [TcdResource("TNPC020f0000.tcd")]
    [TcdResource("TNPC02100000.tcd")]
    [TcdResource("TNPC02110000.tcd")]
    [TcdResource("TNPC02120000.tcd")]
    [TcdResource("TNPC02130000.tcd")]
    [TcdResource("TNPC02140000.tcd")]
    [TcdResource("TNPC02260000.tcd")]
    [TcdResource("TNPC02590000.tcd")]
    [TcdResource("TNPC025a0000.tcd")]
    [TcdResource("TNPC025b0000.tcd")]
    [TcdResource("TNPC025c0000.tcd")]
    [TcdResource("TNPC02bc0000.tcd")]
    [TcdResource("TNPC02bd0000.tcd")]
    [TcdResource("TNPC02be0000.tcd")]
    [TcdResource("TNPC02bf0000.tcd")]
    [TcdResource("TNPC02c00000.tcd")]
    [TcdResource("TNPC02c20000.tcd")]
    [TcdResource("TNPC02c40000.tcd")]
    [TcdResource("TNPC02c60000.tcd")]
    [TcdResource("TNPC02c80000.tcd")]
    [TcdResource("TNPC03210000.tcd")]
    [TcdResource("TNPC03220000.tcd")]
    [TcdResource("TNPC03230000.tcd")]
    [TcdResource("TNPC03240000.tcd")]
    [TcdResource("TNPC038b0000.tcd")]
    [TcdResource("TNPC038c0000.tcd")]
    [TcdResource("TNPC038d0000.tcd")]
    [TcdResource("TNPC038e0000.tcd")]
    [TcdResource("TNPC038f0000.tcd")]
    [TcdResource("TNPC07da0000.tcd")]
    [GenerateSerializer]
    public partial class TNpcCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TNpcEntry> Npcs { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TNpcEntry
    {
        public uint Id { get; set; }
        public ushort TemplateId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Title { get; set; } = string.Empty;

        public byte NpcType { get; set; }
        public byte ClassId { get; set; }
        public byte Level { get; set; }
        public byte CountryId { get; set; }
        public byte CollisionType { get; set; }
        public byte CanBeSelected { get; set; }
        public byte LandType { get; set; }
        public byte Mode { get; set; }
        public byte DrawGhost { get; set; }
        public byte DrawMark { get; set; }
        public byte DrawName { get; set; }
        public byte HouseMesh { get; set; }
        public uint HouseId { get; set; }
        public uint MenuId { get; set; }
        public uint ExecId { get; set; }
        public uint QuestId { get; set; }
        public ushort ItemId { get; set; }
        public uint MaxHp { get; set; }
        public float Direction { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float SizeX { get; set; }
        public float SizeY { get; set; }
        public float SizeZ { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }
        public byte Camp { get; set; }
    }
}
