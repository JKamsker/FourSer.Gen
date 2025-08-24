using FourSer.Contracts;
using Xunit;

namespace FourSer.Tests.Behavioural.UseCases;

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
    [Fact]
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
        var deserialized = ContainerPacket.Deserialize(readOnlySpan);

        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Nested.Id, deserialized.Nested.Id);
    }
}