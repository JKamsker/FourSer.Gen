using System.Reflection;
using FourSer.Tests.OptimizationTesting;
using Microsoft.CodeAnalysis;

namespace FourSer.Tests;

public class OptimizationLevelCompileTests
{
    public static IEnumerable<object[]> GetRepresentativeCasesAndLevels()
    {
        var caseNames = new[]
        {
            "SimplePacket",
            "Collection",
            "EnumerationTypes",
            "MemoryOwner",
            "PolymorphicSingleTypeId",
        };

        foreach (var caseName in caseNames)
        {
            foreach (var optimizationLevel in OptimizationLevels.All)
            {
                yield return new object[] { caseName, optimizationLevel };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetRepresentativeCasesAndLevels))]
    public void ExistingRepresentativeGeneratorCases_ShouldCompileAcrossOptimizationLevels(string testCaseName, string optimizationLevel)
    {
        var source = ReadEmbeddedCase(testCaseName);
        var result = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(optimizationLevel));

        Assert.DoesNotContain(result.RunResult.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        using var assemblyStream = new MemoryStream();
        var emitResult = result.FinalCompilation.Emit(assemblyStream);

        Assert.True(
            emitResult.Success,
            $"Compilation failed for '{testCaseName}' at level '{optimizationLevel}': {string.Join(Environment.NewLine, emitResult.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(static diagnostic => diagnostic.ToString()))}");
    }

    private static string ReadEmbeddedCase(string testCaseName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"FourSer.Tests.GeneratorTestCases.{testCaseName}.input.cs";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
