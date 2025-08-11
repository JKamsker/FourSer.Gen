using Serializer.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serializer.Consumer;

[GenerateSerializer]
public partial class LoginAckPacket
{
    public int A { get; set; }
    public string B { get; set; } = string.Empty;
    public List<int> C { get; set; } = new();
}

[GenerateSerializer]
public partial class MyPacket
{
    public List<byte> Data { get; set; } = new();
}


[GenerateSerializer]
public partial class TestWithCountType
{
   [GenerateSerializer(CountType = typeof(ushort))]
   public List<int> MyList { get; set; } = new();
}

[GenerateSerializer]
public partial class TestWithCountSizeReference
{
   public ushort MyListCount { get; set; }

   [GenerateSerializer(CountSizeReference = "MyListCount")]
   public List<int> MyList { get; set; } = new();
}

[GenerateSerializer]
public partial class NestedPacket
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class ContainerPacket
{
    public NestedPacket Nested { get; set; } = new();
    public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public partial class LoginAckPacketX
{
    public byte bResult;
    public uint dwUserID;
    public uint dwKickID;
    public uint dwKEY;
    public uint Address;
    public ushort Port;
    public byte bCreateCardCnt;
    public byte bInPcRoom;
    public uint dwPremiumPcRoom;
    public long dCurrentTime;
    public long dKey;
}


public class TestCases
{
    public void LoginAckPacketTest()
    {
        var original = new LoginAckPacket
        {
            A = 1,
            B = "Hello",
            C = new List<int> { 1, 2, 3 }
        };

        var size = LoginAckPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        LoginAckPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = LoginAckPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(original.A, deserialized.A);
        Assert.AreEqual(original.B, deserialized.B);
        Assert.AreEqual(true, original.C.SequenceEqual(deserialized.C));
    }

    public void MyPacketImplicitCountTest()
    {
        var original = new MyPacket
        {
            Data = new List<byte> { 0xDE, 0xAD, 0xBE, 0xEF }
        };

        var size = MyPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        MyPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = MyPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(true, original.Data.SequenceEqual(deserialized.Data));
    }

    public void MyPacketWithCountTypeTest()
    {
        var original = new TestWithCountType
        {
            MyList = new List<int> { 10, 20, 30, 40, 50 }
        };

        var size = TestWithCountType.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithCountType.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithCountType.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(true, original.MyList.SequenceEqual(deserialized.MyList));
    }

    public void MyPacketWithCountSizeReferenceTest()
    {
        var original = new TestWithCountSizeReference
        {
            MyList = new List<int> { 100, 200, 300 }
        };
        original.MyListCount = (ushort)original.MyList.Count;

        var size = TestWithCountSizeReference.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        TestWithCountSizeReference.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = TestWithCountSizeReference.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(original.MyListCount, deserialized.MyListCount);
        Assert.AreEqual(true, original.MyList.SequenceEqual(deserialized.MyList));
    }

    public void NestedObjectTest()
    {
        var original = new ContainerPacket
        {
            Nested = new NestedPacket { Id = 42 },
            Name = "Container"
        };

        var size = ContainerPacket.GetPacketSize(original);
        var buffer = new byte[size];
        var span = new Span<byte>(buffer);
        ContainerPacket.Serialize(original, span);
        var readOnlySpan = new ReadOnlySpan<byte>(buffer);
        var deserialized = ContainerPacket.Deserialize(readOnlySpan, out _);

        Assert.AreEqual(original.Name, deserialized.Name);
        Assert.AreEqual(original.Nested.Id, deserialized.Nested.Id);
    }
}