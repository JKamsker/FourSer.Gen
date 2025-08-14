using Serializer.Contracts;

namespace Serializer.Package.Tests;

[GenerateSerializer]
public partial class MyTestPacket
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
