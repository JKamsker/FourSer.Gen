using System.Runtime.Loader;
using FourSer.Gen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FourSer.Tests.OptimizationTesting;

internal static class OptimizationTestCompiler
{
    private const string RootBridgeNamespace = "FourSer.Tests.RuntimeBridge";

    public static GeneratedCompilationResult Generate(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        return Generate([source], globalOptions);
    }

    public static GeneratedCompilationResult Generate(
        IReadOnlyCollection<string> sources,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        var compilation = CreateCompilation(sources);
        var generator = new SerializerGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            parseOptions: compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions,
            optionsProvider: globalOptions is null ? null : new TestAnalyzerConfigOptionsProvider(globalOptions));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        var finalCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);
        var generatedSource = string.Join(
            "\n\n",
            runResult.Results
                .SelectMany(static result => result.GeneratedSources)
                .Where(static sourceResult => sourceResult.HintName.EndsWith("g.cs", StringComparison.Ordinal))
                .Select(static sourceResult => sourceResult.SourceText.ToString()));

        return new GeneratedCompilationResult(runResult, compilation, finalCompilation, generatedSource);
    }

    public static GeneratedAssemblyHandle BuildRuntimeAssembly(
        OptimizationRepresentativeCase testCase,
        string optimizationLevel)
    {
        var compilationResult = Generate(
            [testCase.Source, BuildRuntimeBridgeSource(testCase.RootTypeName)],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.FourSerOptimizationLevel"] = optimizationLevel,
            });

        AssertNoGeneratorErrors(compilationResult.RunResult);

        using var image = new MemoryStream();
        var emitResult = compilationResult.FinalCompilation.Emit(image);
        if (!emitResult.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics
                    .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(static diagnostic => diagnostic.ToString()));

            throw new InvalidOperationException($"Dynamic test assembly emission failed.{Environment.NewLine}{errors}");
        }

        image.Position = 0;
        var loadContext = new AssemblyLoadContext(
            $"FourSerOptimizationTests.{testCase.Name}.{optimizationLevel}",
            isCollectible: true);
        var assembly = loadContext.LoadFromStream(image);
        return new GeneratedAssemblyHandle(loadContext, assembly);
    }

    public static string AddDefaultUsings(string source)
    {
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

    public static IReadOnlyDictionary<string, string> CreateOptions(string optimizationLevel)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["build_property.FourSerOptimizationLevel"] = optimizationLevel,
        };
    }

    private static CSharpCompilation CreateCompilation(IReadOnlyCollection<string> sources)
    {
        var syntaxTrees = Consts.ContractsSource
            .Concat(Consts.ExtensionsSource)
            .Select(static sourceText => CSharpSyntaxTree.ParseText(sourceText))
            .Concat(sources.Select(static sourceText => CSharpSyntaxTree.ParseText(AddDefaultUsings(sourceText))))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: $"FourSer.OptimizationTests.{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: Basic.Reference.Assemblies.Net90.References.All,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static void AssertNoGeneratorErrors(GeneratorDriverRunResult runResult)
    {
        var errors = runResult.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Generator diagnostics contained errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static string BuildRuntimeBridgeSource(string rootTypeName)
    {
        return $$"""
        namespace {{RootBridgeNamespace}};

        public static class PacketRuntimeBridge
        {
            public static global::{{rootTypeName}} CreateSample()
            {
                return global::{{rootTypeName}}.CreateSample();
            }

            public static byte[] SerializeSpan(global::{{rootTypeName}} value)
            {
                var buffer = new byte[global::{{rootTypeName}}.GetPacketSize(value)];
                global::{{rootTypeName}}.Serialize(value, buffer);
                return buffer;
            }

            public static byte[] SerializeStream(global::{{rootTypeName}} value)
            {
                using var stream = new MemoryStream();
                global::{{rootTypeName}}.Serialize(value, stream);
                return stream.ToArray();
            }

            public static byte[] RoundTripSpan(global::{{rootTypeName}} value)
            {
                var data = SerializeSpan(value);
                var roundTripped = global::{{rootTypeName}}.Deserialize(data);
                try
                {
                    return SerializeSpan(roundTripped);
                }
                finally
                {
                    if (roundTripped is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            public static byte[] RoundTripStream(global::{{rootTypeName}} value)
            {
                var data = SerializeStream(value);
                using var stream = new MemoryStream(data, writable: false);
                var roundTripped = global::{{rootTypeName}}.Deserialize(stream);
                try
                {
                    return SerializeStream(roundTripped);
                }
                finally
                {
                    if (roundTripped is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            public static void DeserializeSpan(byte[] data)
            {
                var value = global::{{rootTypeName}}.Deserialize(data);
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            public static void DeserializeStream(byte[] data)
            {
                using var stream = new MemoryStream(data, writable: false);
                var value = global::{{rootTypeName}}.Deserialize(stream);
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        """;
    }
}
