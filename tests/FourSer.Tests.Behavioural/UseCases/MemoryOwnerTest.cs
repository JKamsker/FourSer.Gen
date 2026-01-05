using System.Buffers;
using FourSer.Contracts;

namespace FourSer.Tests.Behavioural.UseCases;

[GenerateSerializer]
public partial class TestMemoryOwner
{
    public IMemoryOwner<byte> Test { get; set; }
}

[GenerateSerializer]
public partial class MemoryOwnerTestWrapper
{
    public TestMemoryOwner TestMemoryOwner { get; set; }
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

public class MemoryOwnerTests
{
    [Fact]
    public void MemoryOwnerIsDisposeable()
    {
        var memoryOwnerIsDisposeable = IsIDisposeable<TestMemoryOwner>();
        Assert.True(memoryOwnerIsDisposeable, "Generated class 'TestMemoryOwner' does not implement IDisposable as expected.");
    }

    [Fact]
    public void MemoryOwnerWrapperIsDisposeable()
    {
        var wrapperIsDisposeable = IsIDisposeable<MemoryOwnerTestWrapper>();
        Assert.True(wrapperIsDisposeable, "Generated class 'MemoryOwnerTestWrapper' does not implement IDisposable as expected.");
    }

    [Fact]
    public void NoMemoryOwnerWrapperIsNotDisposeable()
    {
        var wrapperIsDisposeable = IsIDisposeable<NoMemoryOwnerTestWrapper>();
        Assert.False(wrapperIsDisposeable, "Generated class 'NoMemoryOwnerTestWrapper' implements IDisposable unexpectedly.");
    }

    private static bool IsIDisposeable<T>()
    {
        return typeof(T)
            .GetInterfaces()
            .Contains(typeof(IDisposable));
    }
}
