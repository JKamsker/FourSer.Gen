using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace FourSer.Tests.GeneratorTestCases.UnlimitedCollection;

public class Tests
{
    [Fact]
    public void TestUnlimitedCollection()
    {
        var packet = new UnlimitedCollectionPacket
        {
            Items = new List<UnlimitedItem>
            {
                new UnlimitedItem { Value = 1 },
                new UnlimitedItem { Value = 2 },
                new UnlimitedItem { Value = 3 }
            }
        };

        var buffer = new byte[1024];
        var bytesWritten = UnlimitedCollectionPacket.Serialize(packet, buffer);
        var deserialized = UnlimitedCollectionPacket.Deserialize(buffer.AsSpan(0, bytesWritten), out _);

        deserialized.Items.Should().HaveCount(3);
        deserialized.Items.Select(x => x.Value).Should().Equal(1, 2, 3);
    }
}
