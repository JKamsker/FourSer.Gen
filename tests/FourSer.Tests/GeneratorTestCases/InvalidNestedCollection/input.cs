using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.TestUser
{
    // Note: [GenerateSerializer] is intentionally missing to trigger the error.
    public partial class NestedPacket
    {
        public int Id { get; set; }
    }

    [GenerateSerializer]
    public partial class ContainerPacket
    {
        public List<NestedPacket> Nested { get; set; } = new();
        public string Name { get; set; } = string.Empty;
    }
}
