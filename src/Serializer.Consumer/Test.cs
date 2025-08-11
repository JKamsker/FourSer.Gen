using Serializer.Contracts;
using System.Collections.Generic;

namespace Serializer.Consumer;

[GenerateSerializer]
public partial class Test
{
    public int A { get; set; }
    public string B { get; set; } = string.Empty;
    public List<int> C { get; set; } = new();
}