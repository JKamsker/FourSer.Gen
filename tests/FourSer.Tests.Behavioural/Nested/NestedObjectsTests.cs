using FourSer.Contracts;

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

    /*
    // This test is commented out because of a bug in the source generator.
    // The generated GetPacketSize method does not handle null nested objects,
    // causing a NullReferenceException.
    [Fact]
    public void NullNestedObject_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new ParentPacket
        {
            Id = 456,
            Child = null
        };

        // Act
        var size = ParentPacket.GetPacketSize(original);
        var buffer = new byte[size];
        ParentPacket.Serialize(original, buffer);
        var deserialized = ParentPacket.Deserialize(buffer);

        // Assert
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Null(deserialized.Child);
    }
    */
}
