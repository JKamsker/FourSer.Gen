using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TEquipCreateChar.tcd")]
    [GenerateSerializer]
    public partial class TEquipCreateCharCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TEquipCreateCharEntry> Entries { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TEquipCreateCharEntry
    {
        private const int EquipSlotCount = 9;

        public byte Country { get; set; }
        public byte Class { get; set; }
        public byte Sex { get; set; }

        [SerializeCollection(CountSize = EquipSlotCount)]
        public TEquipCreateCharSlot[] Slots { get; set; } = CreateDefaultSlots();

        private static TEquipCreateCharSlot[] CreateDefaultSlots()
        {
            var slots = new TEquipCreateCharSlot[EquipSlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new TEquipCreateCharSlot();
            }

            return slots;
        }
    }

    [GenerateSerializer]
    public partial class TEquipCreateCharSlot
    {
        public ushort ItemId { get; set; }
        public ushort Grade { get; set; }
        public byte GradeEffect { get; set; }
    }
}
