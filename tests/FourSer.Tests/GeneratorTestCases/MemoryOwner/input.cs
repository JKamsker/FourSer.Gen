using System.Buffers;

namespace FourSer.Tests.GeneratorTestCases.MemoryOwner;

[GenerateSerializer]
public partial class Child
{
    public IMemoryOwner<int>? Numbers { get; set; }
}

[GenerateSerializer]
public partial class Parent
{
    public IMemoryOwner<byte>? Data { get; set; }
    public Child? DisposableProperty { get; set; }
}

