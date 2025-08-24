using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FourSer.Gen;
using System.Reflection;

namespace FourSer.Tests;

public class GeneratorTests
{
    private static readonly string[] s_contractsSource = new[]
    {
        @"
using System;
namespace FourSer.Contracts;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class GenerateSerializerAttribute : Attribute { }
",
        @"
using System;
namespace FourSer.Contracts;
public interface ISerializable<T> where T : ISerializable<T>
{
    static abstract int GetPacketSize(T obj);
    static abstract int Serialize(T obj, Span<byte> data);
    static abstract T Deserialize(ref ReadOnlySpan<byte> data);
    static abstract T Deserialize(ReadOnlySpan<byte> data);
}
",
        @"
namespace FourSer.Contracts;
public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }
",
        @"
using System;
namespace FourSer.Contracts;
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
namespace FourSer.Contracts;
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

    private static readonly string[] s_extensionsSource = new[]
    {
        @"
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers.Binary;
namespace FourSer.Consumer.Extensions;
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
namespace FourSer.Consumer.Extensions;
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
    public static int ReadInt32(this ref ReadOnlySpan<byte> input)
    {
        var val = BinaryPrimitives.ReadInt32LittleEndian(input);
        input = input.Slice(sizeof(int));
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
    public static ushort ReadUInt16(this ref ReadOnlySpan<byte> input)
    {
        var val = BinaryPrimitives.ReadUInt16LittleEndian(input);
        input = input.Slice(sizeof(ushort));
        return val;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingle(this ref ReadOnlySpan<byte> input)
    {
        var val = BinaryPrimitives.ReadSingleLittleEndian(input);
        input = input.Slice(sizeof(float));
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
namespace FourSer.Consumer.Extensions;
public static class SpanWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this ref Span<byte> input, byte value)
    {
        input[0] = value;
        input = input.Slice(1);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32(this ref Span<byte> input, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(input, value);
        input = input.Slice(sizeof(int));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32(this ref Span<byte> input, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(input, value);
        input = input.Slice(sizeof(uint));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16(this ref Span<byte> input, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(input, value);
        input = input.Slice(sizeof(ushort));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSingle(this ref Span<byte> input, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(input, value);
        input = input.Slice(sizeof(float));
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

    public static IEnumerable<object[]> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var testCaseFolders = resourceNames
            .Where(name => name.Contains("GeneratorTestCases") && name.EndsWith("input.cs"))
            .Select
            (
                name =>
                {
                    var parts = name.Split('.');
                    return parts[parts.Length - 3];
                }
            )
            .Distinct()
            .Where(name => name != "InvalidNestedCollection");

        foreach (var folder in testCaseFolders)
        {
            yield return new object[] { folder };
        }
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public Task RunGeneratorTest(string testCaseName)
    {
        var source = ReadSource(testCaseName);
       


        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create
        (
            "TestProject",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();

        // Assert
        Assert.False(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        var generatedCodes = result.Results.Single()
            .GeneratedSources
            .Where(g => g.HintName.EndsWith("g.cs"))
            .Select(x => x.SourceText);

        var generatedCode = string.Join("\n\n", generatedCodes.Select(x => x.ToString()));

        return Verify(generatedCode)
                .UseDirectory(Path.Combine("GeneratorTestCases", testCaseName))
                .UseTypeName(testCaseName)
            ;
    }
    
    /// <summary>
    /// This test verifies that the source code produced by the generator compiles successfully.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void GeneratedSource_ShouldCompile(string testCaseName)
    {
        // Arrange
        var source = ReadSource(testCaseName);

        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create(
            assemblyName: $"{testCaseName}.TestAssembly",
            syntaxTrees: syntaxTrees,
            references: Basic.Reference.Assemblies.Net90.References.All,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        
        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        
        // Add the generated syntax trees to the compilation
        var finalCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);
        
        // Attempt to emit the final assembly
        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);

        // Assert
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();
            
            // Fail the test with a descriptive message if compilation fails
            Assert.True(emitResult.Success, $"Compilation failed with errors: \n{string.Join("\n", errors)}");
        }
    }

    private static string ReadSource(string testCaseName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"FourSer.Tests.GeneratorTestCases.{testCaseName}.input.cs";
        string source;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream))
        {
            source = reader.ReadToEnd();
        }

        // source should not be null or empty
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException($"Resource '{resourceName}' not found or is empty.");
        }
        
        return AddDefaultUsings(source);
    }

    private static string AddDefaultUsings(string source)
    {
        var sb = new System.Text.StringBuilder(source);
        
        var requiredUsings = new[]
        {
            "using System;",
            "using System.Collections.Concurrent;",
            "using System.Collections.Generic;",
            "using FourSer.Consumer.Extensions;",
            "using FourSer.Contracts;"
        };

        foreach (var usingStatement in requiredUsings)
        {
            if (!source.Contains(usingStatement, StringComparison.OrdinalIgnoreCase))
            {
                sb.Insert(0, $"{usingStatement}\n");
            }
        }

        if (source.Length != sb.Length)
        {
            source = sb.ToString();
        }

        return source;
    }
}
