using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using FourSer.Gen;
using Xunit;

namespace FourSer.Tests
{
    public static class IncrementalGeneratorTestHelper
    {
        public static void AssertGeneratorIsIncremental(Func<IIncrementalGenerator> generatorFactory, string source, IEnumerable<string> contractSources)
        {
            var syntaxTrees = contractSources.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

            var compilation = CSharpCompilation.Create(
                "TestProject",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            var generator = generatorFactory();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

            // Run the generator for the first time
            driver = driver.RunGenerators(compilation);
            var result1 = driver.GetRunResult();

            // Introduce a trivial change to the compilation
            var oldSyntaxTree = compilation.SyntaxTrees.First(t => string.IsNullOrEmpty(t.FilePath));
            var newSource = source + "\npublic class Unused { }";
            var newSyntaxTree = CSharpSyntaxTree.ParseText(newSource, path: oldSyntaxTree.FilePath);
            var newCompilation = compilation.ReplaceSyntaxTree(oldSyntaxTree, newSyntaxTree);

            // Run the generator for the second time
            driver = driver.RunGenerators(newCompilation);
            var result2 = driver.GetRunResult();

            // Assert that the second run was incremental
            var trackedSteps = new[]
            {
                "TypesWithGenerateSerializerAttribute",
                "NonNullableTypes",
                "AllSerializers",
                "CollectedSerializers",
                "DistinctSerializers"
            };

            foreach (var step in trackedSteps)
            {
                var trackedStep = result2.Results[0].TrackedSteps[step];
                Assert.All(trackedStep.SelectMany(x => x.Outputs), (output) =>
                {
                    Assert.True(output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged);
                });
            }
        }
    }
}
