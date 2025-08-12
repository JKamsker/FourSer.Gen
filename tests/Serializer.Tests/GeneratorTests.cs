using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serializer.Generator;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Serializer.Tests;

public class GeneratorTests
{
    private readonly string[] contractsSource = new[]
    {
        @"
using System;
namespace Serializer.Contracts;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute { }
",
        @"
using System;
namespace Serializer.Contracts;
public interface ISerializable<T> where T : ISerializable<T>
{
    static abstract int GetPacketSize(T obj);
    static abstract T Deserialize(ReadOnlySpan<byte> data, out int bytesRead);
    static abstract int Serialize(T obj, Span<byte> data);
}
",
        @"
namespace Serializer.Contracts;
public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }
",
        @"
using System;
namespace Serializer.Contracts;
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializeCollectionAttribute : Attribute
{
    public Type? CountType { get; set; }
    public int CountSize { get; set; } = -1;
    public string? CountSizeReference { get; set; }
    public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
    public Type? TypeIdType { get; set; }
    public string? TypeIdProperty { get; set; }
}
",
        @"
using System;
namespace Serializer.Contracts;
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializePolymorphicAttribute : Attribute
{
    public string? PropertyName { get; set; }
    public Type? TypeIdType { get; set; }
    public SerializePolymorphicAttribute(string? propertyName = null) { PropertyName = propertyName; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class PolymorphicOptionAttribute : Attribute
{
    public object Id { get; }
    public Type Type { get; }
    public PolymorphicOptionAttribute(int id, Type type) { Id = id; Type = type; }
    public PolymorphicOptionAttribute(byte id, Type type) { Id = id; Type = type; }
    public PolymorphicOptionAttribute(ushort id, Type type) { Id = id; Type = type; }
    public PolymorphicOptionAttribute(long id, Type type) { Id = id; Type = type; }
    public PolymorphicOptionAttribute(object id, Type type) { Id = id; Type = type; }
}
"
    };

    private readonly string[] extensionsSource = new[]
    {
        @"
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers.Binary;
namespace Serializer.Consumer.Extensions;
public static class StringEx
{
    private static readonly Encoding Utf8Encoding = Encoding.UTF8;
    public static int MeasureSize(string value)
    {
        if (string.IsNullOrEmpty(value)) { return sizeof(int); }
        return sizeof(int) + Utf8Encoding.GetByteCount(value);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref ReadOnlySpan<byte> input)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(input);
        input = input.Slice(sizeof(int));
        if (length == 0) { return string.Empty; }
        var strSpan = input.Slice(0, length);
        input = input.Slice(length);
        return Utf8Encoding.GetString(strSpan);
    }
}",
        @"
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
namespace Serializer.Consumer.Extensions;
public static class RoSpanReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(this ref ReadOnlySpan<byte> input)
    {
        var val = input[0];
        input = input.Slice(1);
        return val;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(this ref ReadOnlySpan<byte> input)
    {
        var val = BinaryPrimitives.ReadUInt32LittleEndian(input);
        input = input.Slice(sizeof(uint));
        return val;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(this ref ReadOnlySpan<byte> input) => StringEx.ReadString(ref input);
}",
        @"
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
namespace Serializer.Consumer.Extensions;
public static class SpanWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this ref Span<byte> input, byte value)
    {
        input[0] = value;
        input = input.Slice(1);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32(this ref Span<byte> input, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(input, value);
        input = input.Slice(sizeof(uint));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(this ref Span<byte> input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            BinaryPrimitives.WriteInt32LittleEndian(input, 0);
            input = input.Slice(sizeof(int));
            return;
        }
        var byteCount = Encoding.UTF8.GetByteCount(value);
        BinaryPrimitives.WriteInt32LittleEndian(input, byteCount);
        input = input.Slice(sizeof(int));
        Encoding.UTF8.GetBytes(value, input);
        input = input.Slice(byteCount);
    }
}"
    };

    [Fact]
    public void SimplePacket_ShouldGenerateCorrectCode()
    {
        // Arrange
        var source = @"
using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

[GenerateSerializer]
public partial class LoginPacket
{
    public byte Result;
    public uint UserID;
    public string Username;
}";

        var expectedGeneratedCode = @"// <auto-generated/>
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

public partial class LoginPacket : ISerializable<LoginPacket>
{
    public static int GetPacketSize(LoginPacket obj)
    {
        var size = 0;
        size += sizeof(byte); // Size for unmanaged type Result
        size += sizeof(uint); // Size for unmanaged type UserID
        size += StringEx.MeasureSize(obj.Username); // Size for string Username
        return size;
    }

    public static LoginPacket Deserialize(ReadOnlySpan<byte> data, out int bytesRead)
    {
        bytesRead = 0;
        var originalData = data;
        var obj = new LoginPacket();
        obj.Result = data.ReadByte();
        obj.UserID = data.ReadUInt32();
        obj.Username = data.ReadString();
        bytesRead = originalData.Length - data.Length;
        return obj;
    }

    public static int Serialize(LoginPacket obj, Span<byte> data)
    {
        var originalData = data;
        data.WriteByte(obj.Result);
        data.WriteUInt32(obj.UserID);
        data.WriteString(obj.Username);
        return originalData.Length - data.Length;
    }
}
";
        var syntaxTrees = contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create("TestProject", syntaxTrees, Basic.Reference.Assemblies.Net90.References.All, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Assert
        Assert.False(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        var generatedCode = result.Results.Single().GeneratedSources.Single().SourceText.ToString();
        Assert.Equal(expectedGeneratedCode.ReplaceLineEndings(), generatedCode.ReplaceLineEndings());
    }

    [Fact]
    public void NestedObject_ShouldGenerateCorrectCode()
    {
        // Arrange
        var source = @"
using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

[GenerateSerializer]
public partial class ContainerPacket
{
    public int Id;
    public NestedData Data;
}

[GenerateSerializer]
public partial class NestedData
{
    public string Name;
    public float Value;
}
";

        var expectedGeneratedCode = @"// <auto-generated/>
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

public partial class ContainerPacket : ISerializable<ContainerPacket>
{
    public static int GetPacketSize(ContainerPacket obj)
    {
        var size = 0;
        size += sizeof(int); // Size for unmanaged type Id
        size += NestedData.GetPacketSize(obj.Data);
        return size;
    }

    public static ContainerPacket Deserialize(ReadOnlySpan<byte> data, out int bytesRead)
    {
        bytesRead = 0;
        var originalData = data;
        var obj = new ContainerPacket();
        obj.Id = data.ReadInt32();
        obj.Data = NestedData.Deserialize(data, out var nestedBytesRead);
        data = data.Slice(nestedBytesRead);
        bytesRead = originalData.Length - data.Length;
        return obj;
    }

    public static int Serialize(ContainerPacket obj, Span<byte> data)
    {
        var originalData = data;
        data.WriteInt32(obj.Id);
        var bytesWritten = NestedData.Serialize(obj.Data, data);
        data = data.Slice(bytesWritten);
        return originalData.Length - data.Length;
    }
}
";
        var syntaxTrees = contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create("TestProject", syntaxTrees, Basic.Reference.Assemblies.Net90.References.All, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Assert
        Assert.False(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSources = result.Results.SelectMany(r => r.GeneratedSources).ToList();
        Assert.Equal(2, generatedSources.Count);

        var containerPacketGenerated = generatedSources.First(g => g.HintName.Contains("ContainerPacket")).SourceText.ToString();
        Assert.Equal(expectedGeneratedCode.ReplaceLineEndings(), containerPacketGenerated.ReplaceLineEndings());
    }

    [Fact]
    public void Collection_ShouldGenerateCorrectCode()
    {
        // Arrange
        var source = @"
using Serializer.Contracts;
using Serializer.Consumer.Extensions;
using System.Collections.Generic;

namespace TestNamespace;

[GenerateSerializer]
public partial class CollectionPacket
{
    [SerializeCollection]
    public List<int> Numbers { get; set; }
}
";

        var expectedGeneratedCode = @"// <auto-generated/>
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Serializer.Contracts;
using Serializer.Consumer.Extensions;

namespace TestNamespace;

public partial class CollectionPacket : ISerializable<CollectionPacket>
{
    public static int GetPacketSize(CollectionPacket obj)
    {
        var size = 0;
        size += sizeof(int); // Default count size for Numbers
        size += obj.Numbers.Count * sizeof(int);
        return size;
    }

    public static CollectionPacket Deserialize(ReadOnlySpan<byte> data, out int bytesRead)
    {
        bytesRead = 0;
        var originalData = data;
        var obj = new CollectionPacket();
        var NumbersCount = 0;
        NumbersCount = data.ReadInt32();
        obj.Numbers = new System.Collections.Generic.List<int>(NumbersCount);
        for (int i = 0; i < NumbersCount; i++)
        {
            obj.Numbers.Add(data.ReadInt32());
        }
        bytesRead = originalData.Length - data.Length;
        return obj;
    }

    public static int Serialize(CollectionPacket obj, Span<byte> data)
    {
        var originalData = data;
        data.WriteInt32(obj.Numbers.Count);
        for (int i = 0; i < obj.Numbers.Count; i++)
        {
            data.WriteInt32(obj.Numbers[i]);
        }
        return originalData.Length - data.Length;
    }
}
";
        var syntaxTrees = contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create("TestProject", syntaxTrees, Basic.Reference.Assemblies.Net90.References.All, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Assert
        Assert.False(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));

        var generatedCode = result.Results.Single().GeneratedSources.Single().SourceText.ToString();
        Assert.Equal(expectedGeneratedCode.ReplaceLineEndings(), generatedCode.ReplaceLineEndings());
    }
}
