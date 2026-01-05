using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using FourSer.Gen.CodeGenerators;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ConstructorInfo = FourSer.Gen.Models.ConstructorInfo;

namespace FourSer.Gen;

[Generator]
public class SerializerGenerator : IIncrementalGenerator
{
    private const string GeneratorErrorDiagnosticId = "FSG0001";
    private const string GeneratorConfigurationErrorDiagnosticId = "FSG0002";

    private static readonly DiagnosticDescriptor s_generatorErrorRule = new DiagnosticDescriptor
    (
        GeneratorErrorDiagnosticId,
        title: "FourSer source generator error",
        messageFormat: "FourSer failed to generate '{0}': {1}",
        category: "FourSer.Gen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_generatorConfigurationErrorRule = new DiagnosticDescriptor
    (
        GeneratorConfigurationErrorDiagnosticId,
        title: "FourSer generation skipped",
        messageFormat: "FourSer skipped generating '{0}': {1}",
        category: "FourSer.Gen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private readonly record struct GenerationResult(TypeToGenerate? Type, GeneratorError? Error);

    private readonly record struct GeneratorError(string TypeName, string Message, string FilePath, TextSpan Span, LinePositionSpan LineSpan)
    {
        public Location DiagnosticLocation => string.IsNullOrEmpty(FilePath)
            ? Microsoft.CodeAnalysis.Location.None
            : Microsoft.CodeAnalysis.Location.Create(FilePath, Span, LineSpan);
    }

    public static bool BenchmarkMode { get; set; }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddHelpers);

        var generationResults = context.SyntaxProvider
            .ForAttributeWithMetadataName
            (
                "FourSer.Contracts.GenerateSerializerAttribute",
                (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
                static (x, ct) => GetSemanticTargetForGenerationSafe(x, ct)
            )
            .WithTrackingName("TypesWithGenerateSerializerAttribute");

        var generatorErrors = generationResults
            .Where(static r => r.Error is not null)
            .Select((r, _) => r.Error!.Value)
            .WithTrackingName("GeneratorErrors");

        context.RegisterSourceOutput(generatorErrors, static (spc, error) => spc.ReportDiagnostic(Diagnostic.Create(s_generatorErrorRule, error.DiagnosticLocation, error.TypeName, error.Message)));

        var nonNullableTypes = generationResults
            .Select((r, _) => r.Type)
            .Where(static t => t is not null)
            .Select((m, _) => m!)
            .WithTrackingName("NonNullableTypes");

        context.RegisterSourceOutput
        (
            nonNullableTypes,
            (spc, source) => Execute(spc, source)
        );

        var allSerializers = nonNullableTypes
            .SelectMany
            (
                (type, _) =>
                {
                    var fromMembers = type.Members
                        .Select(m => m.CustomSerializer)
                        .Where(s => s is not null)
                        .Select(s => s!.Value.SerializerTypeName);
                    var fromDefaults = type.DefaultSerializers
                        .Select(d => d.SerializerTypeName);
                    return fromMembers.Concat(fromDefaults);
                }
            )
            .Collect()
            .Select((serializers, _) => serializers.Distinct().ToEquatableArray())
            .WithTrackingName("AllSerializers");

        context.RegisterSourceOutput(allSerializers, (spc, serializers) => GenerateSerializerCache(spc, serializers.Array));
    }

    private static GenerationResult GetSemanticTargetForGenerationSafe(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        try
        {
            return new GenerationResult(TypeInfoProvider.GetSemanticTargetForGeneration(context, ct), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var location = context.TargetNode.GetLocation();
            var lineSpan = location.GetLineSpan();
            var typeName = context.TargetSymbol.ToDisplayString();
            return new GenerationResult
            (
                null,
                new GeneratorError
                (
                    typeName,
                    ex.ToString(),
                    lineSpan.Path,
                    location.SourceSpan,
                    lineSpan.Span
                )
            );
        }
    }

    private static void GenerateSerializerCache(SourceProductionContext context, ImmutableArray<string> serializers)
    {
        try
        {
            if (serializers.IsEmpty)
            {
                return;
            }

            var sb = new IndentedStringBuilder();
            sb.WriteLine("// <auto-generated/>");
            sb.WriteLine();
            sb.WriteLine("namespace FourSer.Generated.Internal");
            using (sb.BeginBlock())
            {
                sb.WriteLine("/// <summary>");
                sb.WriteLine("/// This class is internal to the FourSer source generator.");
                sb.WriteLine("/// It is not intended for direct use and may change without notice.");
                sb.WriteLine("/// </summary>");
                sb.WriteLine
                    ("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
                sb.WriteLine("internal static class __FourSer_Generated_Serializers");
                using (sb.BeginBlock())
                {
                    foreach (var serializer in serializers)
                    {
                        var fieldName = SanitizeTypeName(serializer);
                        sb.WriteLineFormat("public static readonly {0} {1} = new();", serializer, fieldName);
                    }
                }
            }

            context.AddSource("__FourSer_Generated_Serializers.g.cs", sb.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_generatorErrorRule, Location.None, "__FourSer_Generated_Serializers", ex.ToString()));
        }
    }

    public static string SanitizeTypeName(string typeName)
    {
        return typeName.Replace('.', '_').Replace('<', '_').Replace('>', '_');
    }

    private static void Execute(SourceProductionContext context, TypeToGenerate typeToGenerate)
    {
        if (BenchmarkMode)
        {
            return;
        }

        try
        {
            if (HasInvalidCollection(context, typeToGenerate) || HasInvalidPolymorphicConfiguration(context, typeToGenerate))
            {
                return;
            }

            var sb = new IndentedStringBuilder();

            GenerateFileHeader(sb, typeToGenerate);
            GenerateClassDeclaration(sb, typeToGenerate);

            using (sb.BeginBlock())
            {
                PacketSizeGenerator.GenerateGetSize(sb, typeToGenerate);
                sb.WriteLine();

                if (typeToGenerate.Constructor is { ShouldGenerate: true } ctor && !typeToGenerate.IsRecord)
                {
                    if (!ctor.Parameters.IsEmpty)
                    {
                        GenerateConstructor(sb, typeToGenerate, ctor);
                        sb.WriteLine();
                    }

                    if (!ctor.HasParameterlessConstructor)
                    {
                        GenerateParameterlessConstructor(sb, typeToGenerate);
                        sb.WriteLine();
                    }
                }

                DeserializationGenerator.GenerateDeserialize(sb, typeToGenerate);
                sb.WriteLine();

                SerializationGenerator.GenerateSerialize(sb, typeToGenerate);

                DisposalGenerator.GenerateDispose(sb, typeToGenerate);

                if (!typeToGenerate.NestedTypes.IsEmpty)
                {
                    NestedTypeGenerator.GenerateNestedTypes(sb, typeToGenerate.NestedTypes);
                }
            }

            var hintNameWithoutExtension = string.IsNullOrEmpty(typeToGenerate.Namespace)
                ? typeToGenerate.Name
                : $"{typeToGenerate.Namespace}.{typeToGenerate.Name}";
            var hintName = $"{hintNameWithoutExtension.Replace('.', '_')}.g.cs";

            context.AddSource(hintName, sb.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic
                (Diagnostic.Create(s_generatorErrorRule, Location.None, GetTypeDisplayName(typeToGenerate), ex.ToString()));
        }
    }

    private static string GetTypeDisplayName(TypeToGenerate typeToGenerate)
    {
        return string.IsNullOrEmpty(typeToGenerate.Namespace)
            ? typeToGenerate.Name
            : $"{typeToGenerate.Namespace}.{typeToGenerate.Name}";
    }

    private static void GenerateFileHeader(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLine("// <auto-generated/>");
        sb.WriteLine("using System;");
        sb.WriteLine("using System.Buffers;");
        sb.WriteLine("using System.Buffers.Binary;");
        sb.WriteLine("using System.Collections.Generic;");
        sb.WriteLine("using System.Linq;");
        sb.WriteLine("using System.Runtime.CompilerServices;");
        sb.WriteLine("using System.Text;");
        sb.WriteLine("using FourSer.Contracts;");
        sb.WriteLine("using System.IO;");
        sb.WriteLine("using FourSer.Gen.Helpers;");
        sb.WriteLine("using SpanReader = FourSer.Gen.Helpers.RoSpanReaderHelpers;");
        sb.WriteLine("using StreamReader = FourSer.Gen.Helpers.StreamReaderHelpers;");
        sb.WriteLine("using SpanWriter = FourSer.Gen.Helpers.SpanWriterHelpers;");
        sb.WriteLine("using StreamWriter = FourSer.Gen.Helpers.StreamWriterHelpers;");
        sb.WriteLine();
        if (!string.IsNullOrEmpty(typeToGenerate.Namespace))
        {
            sb.WriteLineFormat("namespace {0};", typeToGenerate.Namespace);
            sb.WriteLine();
        }
    }

    private static void GenerateClassDeclaration(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var typeKeyword = typeToGenerate.IsValueType ? "struct" : "class";
        if (typeToGenerate.IsRecord)
        {
            typeKeyword = $"record {typeKeyword}";
        }

        var disposableInterface = DisposalGenerator.ShouldGenerateDispose(typeToGenerate) ? ", IDisposable" : string.Empty;
        sb.WriteLineFormat("public partial {0} {1} : ISerializable<{1}>{2}", typeKeyword, typeToGenerate.Name, disposableInterface);
    }

    internal static void GenerateConstructor(IndentedStringBuilder sb, TypeToGenerate typeToGenerate, ConstructorInfo ctor)
    {
        var ctorBuilder = new StringBuilder();
        ctorBuilder.AppendFormat("private {0}(", typeToGenerate.Name);

        var first = true;
        foreach (var p in ctor.Parameters)
        {
            if (!first)
            {
                ctorBuilder.Append(", ");
            }

            ctorBuilder.Append(p.TypeName);
            ctorBuilder.Append(' ');
            ctorBuilder.Append(p.Name.ToCamelCase());
            first = false;
        }

        ctorBuilder.Append(')');
        sb.WriteLine(ctorBuilder.ToString());

        using var _ = sb.BeginBlock();
        foreach (var parameter in ctor.Parameters)
        {
            sb.WriteLineFormat("this.{0} = {1};", parameter.Name, parameter.Name.ToCamelCase());
        }
    }

    internal static void GenerateParameterlessConstructor(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        if (typeToGenerate.IsValueType)
        {
            return;
        }

        sb.WriteLineFormat("public {0}()", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsReadOnly)
            {
                continue;
            }

            sb.WriteLineFormat("this.{0} = default;", member.Name);
        }
    }

    private static bool HasInvalidCollection(SourceProductionContext context, TypeToGenerate typeToGenerate)
    {
        var typeName = GetTypeDisplayName(typeToGenerate);
        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsMemoryOwner)
            {
                if (member.CollectionInfo?.Unlimited == true)
                {
                    context.ReportDiagnostic
                    (
                        Diagnostic.Create
                        (
                            s_generatorConfigurationErrorRule,
                            Location.None,
                            typeName,
                            $"IMemoryOwner<T> member '{member.Name}' is marked as Unlimited, which is not supported."
                        )
                    );
                    return true;
                }

                if (member.CustomSerializer is not null)
                {
                    continue;
                }

                if (member.MemoryOwnerTypeInfo is not { } memoryOwnerTypeInfo)
                {
                    continue;
                }

                if (memoryOwnerTypeInfo.IsElementUnmanagedType
                    || memoryOwnerTypeInfo.IsElementStringType
                    || memoryOwnerTypeInfo.HasElementGenerateSerializerAttribute)
                {
                    continue;
                }

                context.ReportDiagnostic
                (
                    Diagnostic.Create
                    (
                        s_generatorConfigurationErrorRule,
                        Location.None,
                        typeName,
                        $"IMemoryOwner<T> member '{member.Name}' has unsupported element type '{memoryOwnerTypeInfo.ElementTypeName}'. Add [GenerateSerializer] to the element type or apply [Serializer(...)] to the member."
                    )
                );
                return true;
            }

            if (!member.IsCollection || member.CollectionTypeInfo is null)
            {
                continue;
            }

            if (member.CustomSerializer is not null)
            {
                continue;
            }

            var collectionTypeInfo = member.CollectionTypeInfo.Value;
            if (collectionTypeInfo.IsElementUnmanagedType || collectionTypeInfo.IsElementStringType ||
                collectionTypeInfo.HasElementGenerateSerializerAttribute)
            {
                continue;
            }

            context.ReportDiagnostic
            (
                Diagnostic.Create
                (
                    s_generatorConfigurationErrorRule,
                    Location.None,
                    typeName,
                    $"Collection member '{member.Name}' has unsupported element type '{collectionTypeInfo.ElementTypeName}'. Add [GenerateSerializer] to the element type or apply [Serializer(...)] to the collection member."
                )
            );
            return true;
        }

        return false;
    }

    private static bool HasInvalidPolymorphicConfiguration(SourceProductionContext context, TypeToGenerate typeToGenerate)
    {
        var typeName = GetTypeDisplayName(typeToGenerate);
        foreach (var member in typeToGenerate.Members)
        {
            var collectionInfo = member.CollectionInfo;

            var requestsPolymorphism =
                (collectionInfo?.PolymorphicMode ?? PolymorphicMode.None) != PolymorphicMode.None
                || !string.IsNullOrEmpty(collectionInfo?.TypeIdProperty)
                || member.PolymorphicInfo is not null;

            if (!requestsPolymorphism)
            {
                continue;
            }

            if (member.IsMemoryOwner)
            {
                context.ReportDiagnostic
                (
                    Diagnostic.Create
                    (
                        s_generatorConfigurationErrorRule,
                        Location.None,
                        typeName,
                        $"IMemoryOwner<T> member '{member.Name}' does not support polymorphic serialization."
                    )
                );
                return true;
            }

            if (member.PolymorphicInfo is not { } info || info.Options.IsEmpty)
            {
                context.ReportDiagnostic
                (
                    Diagnostic.Create
                    (
                        s_generatorConfigurationErrorRule,
                        Location.None,
                        typeName,
                        $"Member '{member.Name}' is configured for polymorphic serialization but has no [PolymorphicOption]s."
                    )
                );
                return true;
            }
        }

        return false;
    }

    private static void AddHelpers(IncrementalGeneratorPostInitializationContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();

        foreach (var file in names)
        {
            if (!file.StartsWith("FourSer.Gen.Resources.Code."))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(file);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            context.AddSource(file, source);
        }
    }
}
