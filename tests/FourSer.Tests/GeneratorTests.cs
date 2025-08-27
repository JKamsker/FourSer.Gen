using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FourSer.Gen;
using System.Reflection;

namespace FourSer.Tests;

public class GeneratorTests
{
    private static readonly string[] s_contractsSource = Consts.ContractsSource;

    private static readonly string[] s_extensionsSource = Consts.ExtensionsSource;

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

        var generator = new SerializerGenerator().AsSourceGenerator();
        var driver =  CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { generator },
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

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
