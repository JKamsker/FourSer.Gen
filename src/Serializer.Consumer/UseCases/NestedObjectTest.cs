using Serializer.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serializer.Consumer;

[GenerateSerializer]
public partial class NestedPacket
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class ContainerPacket
{
    public NestedPacket Nested { get; set; } = new();
    public string Name { get; set; } = string.Empty;
}

public class NestedObjectTest
{
    public void NestedObjectSerializationTest()
    {
        var original = new ContainerPacket
        {
            Nested = new NestedPacket { Id = 42 },
            Name = "Container"
        };

        var size = ContainerPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        ContainerPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = ContainerPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(original.Name, deserialized.Name);
        Assert.AreEqual(original.Nested.Id, deserialized.Nested.Id);
    }
}