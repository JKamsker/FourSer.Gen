using Serializer.Contracts;
using Serializer.Consumer.Extensions;
using System.Collections.Generic;

namespace TestNamespace;

[GenerateSerializer]
public partial class CollectionPacket
{
    [SerializeCollection]
    public List<int> Numbers { get; set; }
}
