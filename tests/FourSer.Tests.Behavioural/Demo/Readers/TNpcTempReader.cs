using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TNPCTemp.tcd")]
    [GenerateSerializer]
    public partial class TNpcTemplateCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TNpcTemplateEntry> Templates { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TNpcTemplateEntry
    {
        public ushort TemplateId { get; set; }
        public uint ObjectId { get; set; }

        [SerializeCollection(CountSize = ReaderConstants.EquipmentSlotCount)]
        public ushort[] EquipmentItemIds { get; set; } = new ushort[ReaderConstants.EquipmentSlotCount];

        public byte ActionId { get; set; }
        public byte RandomAction0 { get; set; }
        public byte RandomAction1 { get; set; }
        public uint SoundId { get; set; }
        public ushort FaceIconId { get; set; }
        public byte BoxAnimationId { get; set; }
    }
}

