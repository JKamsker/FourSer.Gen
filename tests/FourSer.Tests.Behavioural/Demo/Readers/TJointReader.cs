using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.Demo
{
    [TcdResource("TJoint.tcd")]
    [GenerateSerializer]
    public partial class TJointCatalog
    {
        [SerializeCollection(CountType = typeof(ushort))]
        public List<TJointRecord> Records { get; set; } = new();
    }

    [GenerateSerializer]
    public partial class TJointRecord
    {
        public uint RegionId { get; set; }
        public int RectLeft { get; set; }
        public int RectTop { get; set; }
        public int RectRight { get; set; }
        public int RectBottom { get; set; }
        public byte JointType { get; set; }
    }
}

