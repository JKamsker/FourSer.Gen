using FourSer.Contracts;
using System.IO;

namespace FourSer.Tests.Behavioural.Nested;

[GenerateSerializer]
public partial class ParentPacket
{
    public int Id { get; set; }
    public ChildPacket? Child { get; set; }
}

[GenerateSerializer]
public partial class ChildPacket
{
    public string? Name { get; set; }
}

public class NestedObjectsTests
{
    [Fact]
    public void NestedObject_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new ParentPacket
        {
            Id = 123,
            Child = new ChildPacket { Name = "Child" }
        };

        // Act
        var size = ParentPacket.GetPacketSize(original);
        var buffer = new byte[size];
        ParentPacket.Serialize(original, buffer);
        var deserialized = ParentPacket.Deserialize(buffer);

        // Assert
        Assert.Equal(original.Id, deserialized.Id);
        Assert.NotNull(deserialized.Child);
        Assert.Equal(original.Child.Name, deserialized.Child.Name);
    }

    [Fact]
    public void NullNestedObject_ShouldThrowDuringSizingAndSerialization()
    {
        var original = new ParentPacket
        {
            Id = 456,
            Child = null
        };

        Assert.Throws<NullReferenceException>(() => ParentPacket.GetPacketSize(original));
        Assert.Throws<NullReferenceException>(() => ParentPacket.Serialize(original, new byte[32]));

        using var stream = new MemoryStream();
        Assert.Throws<NullReferenceException>(() => ParentPacket.Serialize(original, stream));
    }

    [Fact]
    public void RootNull_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParentPacket.GetPacketSize(null!));
        Assert.Throws<ArgumentNullException>(() => ParentPacket.Serialize(null!, new byte[32]));

        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => ParentPacket.Serialize(null!, stream));
    }
}
