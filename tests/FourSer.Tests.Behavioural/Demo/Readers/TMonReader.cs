using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TMon.tcd")]
    [GenerateSerializer]
    public partial class TMonCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TMonTemplate> Templates { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TMonTemplate
    {
        public ushort MonsterId { get; set; }
        public ushort KindId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Title { get; set; } = string.Empty;

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public float LostBalance { get; set; }
        public float LostState { get; set; }
        public float AttackBalance { get; set; }
        public uint ObjectId { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.EquipmentSlotCount)]
        public ushort[] EquipmentItemIds { get; set; } = new ushort[ReaderConstants.EquipmentSlotCount];

        [SerializeCollection(CountSize = ReaderConstants.MonsterSkillCount)]
        public ushort[] SkillIds { get; set; } = new ushort[ReaderConstants.MonsterSkillCount];

        public byte NotKnockBack { get; set; }
        public byte CanBeSelected { get; set; }
        public byte CanFly { get; set; }
        public byte CanAttack { get; set; }
        public byte DrawName { get; set; }
        public byte CanTame { get; set; }
        public byte Visible { get; set; }
        public byte ApplyAi { get; set; }
        public uint MenuId { get; set; }
        public float Size { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }
        public ushort SpawnSfxId { get; set; }
        public uint SpawnSoundId { get; set; }
        public ushort FaceIconId { get; set; }
        public byte CanDetectHiddenPlayers { get; set; }
        public byte SlidingWhenDieFlag { get; set; }
        public byte DrawNameWhenDieFlag { get; set; }
    }
}
