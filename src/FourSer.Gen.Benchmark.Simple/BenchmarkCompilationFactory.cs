using FourSer.Gen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Gen.Benchmark.Simple;

internal static class BenchmarkCompilationFactory
{
    public static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        var syntaxTrees = ContractSources
            .Concat(ExtensionSources)
            .Concat(sources)
            .Select(static source => CSharpSyntaxTree.ParseText(AddDefaultUsings(source)))
            .ToArray();

        return CSharpCompilation.Create(
            $"FourSer.Benchmark.{Guid.NewGuid():N}",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    public static GeneratorDriver CreateDriver(string optimizationLevel)
    {
        return CSharpGeneratorDriver.Create(
            generators: [new SerializerGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true),
            optionsProvider: new BenchmarkAnalyzerConfigOptionsProvider(optimizationLevel));
    }

    private static string AddDefaultUsings(string source)
    {
        source = source.Replace(
            "[GenerateSerializer]",
            "[GenerateSerializer(SerializerGenerationMethods.Stream)]",
            StringComparison.Ordinal);

        var requiredUsings = new[]
        {
            "using System;",
            "using System.Buffers;",
            "using System.Collections.Concurrent;",
            "using System.Collections.Generic;",
            "using System.Collections.Immutable;",
            "using System.IO;",
            "using System.Linq;",
            "using FourSer.Contracts;",
            "using FourSer.Gen.Helpers;",
        };

        var builder = new System.Text.StringBuilder(source);
        foreach (var usingStatement in requiredUsings)
        {
            if (!source.Contains(usingStatement, StringComparison.OrdinalIgnoreCase))
            {
                builder.Insert(0, $"{usingStatement}\n");
            }
        }

        return builder.ToString();
    }

    private sealed class BenchmarkAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;

        public BenchmarkAnalyzerConfigOptionsProvider(string optimizationLevel)
        {
            _globalOptions = new BenchmarkAnalyzerConfigOptions(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.FourSerOptimizationLevel"] = optimizationLevel,
            });
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => BenchmarkAnalyzerConfigOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => BenchmarkAnalyzerConfigOptions.Empty;
    }

    private sealed class BenchmarkAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static BenchmarkAnalyzerConfigOptions Empty { get; } =
            new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private readonly IReadOnlyDictionary<string, string> _values;

        public BenchmarkAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }

    private static readonly string[] ContractSources =
    [
        """
        using System;
        namespace FourSer.Contracts;
        [Flags]
        public enum SerializerGenerationMethods
        {
            None = 0,
            Stream = 1,
            BufferWriter = 2,
            SequenceReader = 4,
            PipeWriter = 8,
            PipeReader = 16,
        }
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
        public class GenerateSerializerAttribute : Attribute
        {
            public GenerateSerializerAttribute() { }
            public GenerateSerializerAttribute(SerializerGenerationMethods additionalMethods) { AdditionalMethods = additionalMethods; }
            public SerializerGenerationMethods AdditionalMethods { get; set; }
        }
        """,
        """
        using System;
        using System.IO;
        namespace FourSer.Contracts;
        public interface ISerializable<T> where T : ISerializable<T>
        {
            static abstract int GetPacketSize(T obj);
            static abstract void Serialize(T obj, ref Span<byte> data);
            static abstract void Serialize(T obj, Span<byte> data);
            static abstract T Deserialize(ref ReadOnlySpan<byte> data);
            static abstract T Deserialize(ReadOnlySpan<byte> data);
        }
        """,
        """
        namespace FourSer.Contracts;
        public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }
        """,
        """
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
            public bool Unlimited { get; set; }
        }
        """,
        """
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
            public bool IsDefault { get; set; }
            public PolymorphicOptionAttribute(byte id, Type type, bool isDefault = false) { Id = id; Type = type; IsDefault = isDefault; }
            public PolymorphicOptionAttribute(int id, Type type, bool isDefault = false) { Id = id; Type = type; IsDefault = isDefault; }
        }
        """
    ];

    private static readonly string[] ExtensionSources =
    [
        """
        using System;
        using System.Buffers.Binary;
        using System.Text;
        namespace FourSer.Consumer.Extensions;
        public static class StringEx
        {
            public static int MeasureSize(string value) => sizeof(int) + Encoding.UTF8.GetByteCount(value ?? string.Empty);
        }
        """
    ];
}
