using FourSer.Contracts;

namespace FourSer.Consumer;

[GenerateSerializer]
public partial class Data
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class User
{
    public void A()
    {
        var data = new Data
        {
            Id = 1,
            Name = "Test"
        };

        var buffer = new byte[Data.GetPacketSize(data)];
        Data.Serialize(data, buffer);

        var deserializedData = Data.Deserialize(buffer.AsSpan());
    }
}
