using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TSkill.tcd")]
    [GenerateSerializer]
    public partial class TSkillCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TSkillEntry> Skills { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TSkillEntry
    {
        public ushort SkillId { get; set; }

        [Serializer(typeof(MfcAnsiStringSerializer))]
        public string Name { get; set; } = string.Empty;

        public ushort ActionSkillId { get; set; }
        public ushort RequiredItemId { get; set; }
        public ushort DefensiveSkillId { get; set; }
        public uint ClassMask { get; set; }
        public byte Kind { get; set; }
        public byte PositiveFlag { get; set; }
        public byte RequiredLevel { get; set; }
        public byte RequiredLevelIncrement { get; set; }
        public byte MaxLevel { get; set; }
        public float Price { get; set; }
        public byte DurabilitySlot { get; set; }
        public uint WeaponId { get; set; }
        public ushort UseHp { get; set; }
        public ushort UseMp { get; set; }
        public byte HitInitial { get; set; }
        public byte HitIncrement { get; set; }
        public uint SpellTick { get; set; }
        public byte RideFlag { get; set; }
        public uint Delay { get; set; }
        public int DelayIncrement { get; set; }
        public uint GroupTick { get; set; }
        public uint Interval { get; set; }
        public byte DelayType { get; set; }
        public ushort ModeId { get; set; }
        public byte TargetType { get; set; }
        public byte RangeType { get; set; }
        public float MinRange { get; set; }
        public float MaxRange { get; set; }
        public float AttackRange { get; set; }
        public float BufferRange { get; set; }
        public uint Duration { get; set; }
        public uint DurationIncrement { get; set; }
        public byte CanCancel { get; set; }
        public byte ContinueFlag { get; set; }
        public ushort IconId { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.ShotTypeCount)]
        public ushort[] ShotItemIds { get; set; } = new ushort[ReaderConstants.ShotTypeCount];

        public byte ActiveFlag { get; set; }
        public byte LoopFlag { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.SkillActionCount)]
        public byte[] ActionIds { get; set; } = new byte[ReaderConstants.SkillActionCount];

        public uint InfoId { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.SkillSfxCount)]
        public uint[] EffectIds { get; set; } = new uint[ReaderConstants.SkillSfxCount];

        public byte ShowIcon { get; set; }
        public byte ShowTime { get; set; }
        public byte ShowCritical { get; set; }
        public byte UseInHold { get; set; }
        public byte StaticWhenDie { get; set; }
        public float MoveDistance { get; set; }
    }
}
