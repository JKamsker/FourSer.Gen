using FourSer.Contracts;
using FourSer.Gen.Helpers;
using System;

namespace FourSer.Tests.Behavioural.Interop;

[GenerateSerializer]
public partial class SimplePacket
{
    public int PlayerId { get; set; }
    public string? PlayerName { get; set; }
}

public class InteropTests
{
    [Fact]
    public void SimplePacket_CanDeserialize_FromManualWriter()
    {
        // Arrange: Manually create the byte stream
        var expectedPlayerId = 456;
        var expectedPlayerName = "Aragorn";

        var buffer = new byte[sizeof(int) + StringEx.MeasureSize(expectedPlayerName)];
        var span = buffer.AsSpan();

        SpanWriterHelpers.WriteInt32(ref span, expectedPlayerId);
        StringEx.WriteString(ref span, expectedPlayerName);

        // Act: Use the generated deserializer on our manual buffer
        var deserialized = SimplePacket.Deserialize(buffer);

        // Assert
        Assert.Equal(expectedPlayerId, deserialized.PlayerId);
        Assert.Equal(expectedPlayerName, deserialized.PlayerName);
    }

    [Fact]
    public void SimplePacket_SerializedOutput_CanBeReadByManualReader()
    {
        // Arrange: Generate the byte stream using the source generator
        var original = new SimplePacket { PlayerId = 789, PlayerName = "Legolas" };
        var buffer = new byte[SimplePacket.GetPacketSize(original)];
        SimplePacket.Serialize(original, buffer);

        // Act: Use a manual reader to parse the generated buffer
        var span = new ReadOnlySpan<byte>(buffer);
        var actualPlayerId = RoSpanReaderHelpers.ReadInt32(ref span);
        var actualPlayerName = StringEx.ReadString(ref span);

        // Assert
        Assert.Equal(original.PlayerId, actualPlayerId);
        Assert.Equal(original.PlayerName, actualPlayerName);
    }

    [Fact]
    public void SimplePacket_NullPlayerName_ShouldRoundtripCorrectly()
    {
        // Arrange
        var original = new SimplePacket { PlayerId = 123, PlayerName = "" };

        // Act
        var buffer = new byte[SimplePacket.GetPacketSize(original)];
        SimplePacket.Serialize(original, buffer);
        var deserialized = SimplePacket.Deserialize(buffer);

        // Assert
        Assert.Equal(original.PlayerId, deserialized.PlayerId);
        Assert.Equal(original.PlayerName, deserialized.PlayerName);
    }
}
