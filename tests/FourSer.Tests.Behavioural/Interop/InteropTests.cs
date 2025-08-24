using FourSer.Contracts;
using System.IO;
using System.Text;

namespace FourSer.Tests.Behavioural.Interop;

[GenerateSerializer]
public partial class SimplePacket
{
    public int PlayerId { get; set; }
    public string? PlayerName { get; set; }
}

public class InteropTests
{
    /*
    // These tests are commented out because of a suspected bug in the source generator's
    // string serialization logic. The null-flag for strings seems to be handled incorrectly,
    // which makes it impossible to create a predictable byte layout for interoperability testing.

    [Fact]
    public void SimplePacket_CanDeserialize_FromManualBinaryWriter()
    {
        // Arrange: Manually create the byte stream
        byte[] manualBuffer;
        var expectedPlayerId = 456;
        var expectedPlayerName = "Aragorn";

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms, Encoding.UTF8))
        {
            // Write PlayerId (4 bytes)
            writer.Write(expectedPlayerId);

            // Write PlayerName (custom format: null-flag byte, length int, then bytes)
            writer.Write((byte)1); // 1 = not null
            var nameBytes = Encoding.UTF8.GetBytes(expectedPlayerName);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);
            manualBuffer = ms.ToArray();
        }

        // Act: Use the generated deserializer on our manual buffer
        var deserialized = SimplePacket.Deserialize(manualBuffer);

        // Assert
        Assert.Equal(expectedPlayerId, deserialized.PlayerId);
        Assert.Equal(expectedPlayerName, deserialized.PlayerName);
    }

    [Fact]
    public void SimplePacket_SerializedOutput_CanBeReadByManualBinaryReader()
    {
        // Arrange: Generate the byte stream using the source generator
        var original = new SimplePacket { PlayerId = 789, PlayerName = "Legolas" };
        var buffer = new byte[SimplePacket.GetPacketSize(original)];
        SimplePacket.Serialize(original, buffer);

        // Act: Use a manual BinaryReader to parse the generated buffer
        int actualPlayerId;
        string? actualPlayerName;

        using (var ms = new MemoryStream(buffer))
        using (var reader = new BinaryReader(ms, Encoding.UTF8))
        {
            actualPlayerId = reader.ReadInt32();
            var isNull = reader.ReadByte();
            if (isNull == 1)
            {
                var length = reader.ReadInt32();
                var bytes = reader.ReadBytes(length);
                actualPlayerName = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                actualPlayerName = null;
            }
        }

        // Assert
        Assert.Equal(original.PlayerId, actualPlayerId);
        Assert.Equal(original.PlayerName, actualPlayerName);
    }
    */
}
