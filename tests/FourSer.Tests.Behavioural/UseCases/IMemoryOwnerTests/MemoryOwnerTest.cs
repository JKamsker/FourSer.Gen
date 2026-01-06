using System.Buffers;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases.IMemoryOwnerTests;

[GenerateSerializer]
public partial class TestMemoryOwner
{
    [SerializeCollection(CountType = typeof(byte))]
    public IMemoryOwner<byte> Test { get; set; }
}

[GenerateSerializer]
public partial class MemoryOwnerTestWrapper
{
    public TestMemoryOwner TestMemoryOwner { get; set; }
}

[GenerateSerializer]
public partial class MemoryOwnerEnumerableTestWrapper
{
    public IEnumerable<TestMemoryOwner> Items { get; set; }
}

[GenerateSerializer]
public partial class MemoryOwnerListTestWrapper
{
    public List<TestMemoryOwner> Items { get; set; }
}

[GenerateSerializer]
public partial class MemoryOwnerArrayTestWrapper
{
    public TestMemoryOwner[] Items { get; set; }
}


[GenerateSerializer]
public partial class NoMemoryOwnerTestWrapper
{
    public int SomeInt { get; set; }
}

// MemoryOwner but with own IDisposable implementation
[GenerateSerializer]
public partial class CustomDisposeMemoryOwner : IDisposable
{
    public IMemoryOwner<byte> Data { get; set; }
    public void Dispose()
    {
        Data?.Dispose();
    }
}

[GenerateSerializer]
public partial class SerializeCollectionWorksOnIMemoryOwner
{
    public long Size { get; set; }

    [SerializeCollection(CountSizeReference = nameof(Size))]
    public IMemoryOwner<byte> Value { get; set; }
}

public class MemoryOwnerTests
{
    [Theory]
    [InlineData(typeof(TestMemoryOwner), true)]
    [InlineData(typeof(MemoryOwnerTestWrapper), true)]
    [InlineData(typeof(MemoryOwnerEnumerableTestWrapper), true)]
    [InlineData(typeof(MemoryOwnerListTestWrapper), true)]
    [InlineData(typeof(MemoryOwnerArrayTestWrapper), true)]
    [InlineData(typeof(SerializeCollectionWorksOnIMemoryOwner), true)]
    [InlineData(typeof(NoMemoryOwnerTestWrapper), false)]
    public void GeneratedClass_ImplementsIDisposable_AsExpected(Type type, bool expectedDisposable)
    {
        var isDisposable = IsIDisposeable(type);
        
        if (expectedDisposable)
        {
            Assert.True(isDisposable, $"Generated class '{type.Name}' does not implement IDisposable as expected.");
        }
        else
        {
            Assert.False(isDisposable, $"Generated class '{type.Name}' implements IDisposable unexpectedly.");
        }
    }

    private static bool IsIDisposeable(Type type)
    {
        return type
            .GetInterfaces()
            .Contains(typeof(IDisposable));
    }
}
