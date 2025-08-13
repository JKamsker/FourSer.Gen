using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

[GenerateSerializer]
public partial class ContainerPacket
{
    public int Id;
    public NestedData Data;
}

[GenerateSerializer]
public partial class NestedData
{
    public string Name;
    public float Value;
}
