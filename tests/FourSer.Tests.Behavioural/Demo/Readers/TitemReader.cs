using System;
using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    /// <summary>
    /// Represents the entire TItem.tcd file structure.
    /// It contains a list of TItem entries, prefixed by a count of type ushort.
    /// </summary>
    [TcdResource("TItem.tcd")]
    [GenerateSerializer]
    public partial class TItemChart
    {
        /// <summary>
        /// A list of all item templates. The binary file starts with a WORD (ushort)
        /// indicating the number of items, which this attribute handles automatically.
        /// </summary>
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Represents a single item template entry, corresponding to the C++ tagTITEM struct.
    /// </summary>
    [GenerateSerializer]
    public partial class TItem
    {
        public ushort ItemID { get; set; }
        public byte Type { get; set; }
        public byte Kind { get; set; }
        public ushort AttrID { get; set; }
        [Serializer(typeof(MfcStringSerializer))]
        public string Name { get; set; }
        public ushort UseValue { get; set; }
        public uint SlotID { get; set; }
        public uint ClassID { get; set; }
        public byte PrmSlotID { get; set; }
        public byte SubSlotID { get; set; }
        public byte Level { get; set; }
        public byte CanRepair { get; set; }
        public uint DuraMax { get; set; }
        public byte RefineMax { get; set; }
        public float PriceRate { get; set; }
        public uint Price { get; set; }
        public byte MinRange { get; set; }
        public byte MaxRange { get; set; }
        public byte Stack { get; set; }
        public byte SlotCount { get; set; }
        public byte CanGamble { get; set; }
        public byte GambleProb { get; set; }
        public byte DestoryProb { get; set; }
        public byte CanGrade { get; set; }
        public byte CanMagic { get; set; }
        public byte CanRare { get; set; }
        public ushort DelayGroupID { get; set; }
        public uint Delay { get; set; }
        public byte CanTrade { get; set; }
        public byte IsSpecial { get; set; }
        public ushort UseTime { get; set; }
        public byte UseType { get; set; }
        public byte WeaponID { get; set; }
        public float ShotSpeed { get; set; }
        public float Gravity { get; set; }
        public uint InfoID { get; set; }
        public byte SkillItemType { get; set; }

        /// <summary>
        /// Fixed-size array for Visual data (m_wVisual).
        /// Serializes exactly 5 ushorts without a count prefix.
        /// </summary>
        [SerializeCollection(CountSize = 5)]
        public ushort[] Visual { get; set; } = new ushort[5];

        public ushort GradeSFX { get; set; }

        /// <summary>
        /// Fixed-size array for OptionSFX data (m_wOptionSFX).
        /// Serializes exactly 3 ushorts without a count prefix.
        /// </summary>
        [SerializeCollection(CountSize = 3)]
        public ushort[] OptionSFX { get; set; } = new ushort[3];

        public byte CanWrap { get; set; }
        public uint AuctionCode { get; set; }
        public byte CanColor { get; set; }
    }
}

