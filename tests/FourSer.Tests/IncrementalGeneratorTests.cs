using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FourSer.Gen;

namespace FourSer.Tests
{
    public class IncrementalGeneratorTests
    {
        [Fact]
        public void Generator_Should_Be_Incremental()
        {
            var stepWhitelist = new[]
            {
                "TypesWithGenerateSerializerAttribute",
                "NonNullableTypes",
                "AllSerializers"
            };
            
            var generator = new SerializerGenerator();
            var sourceGenerator = generator.AsSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create
            (
                generators: new[] { sourceGenerator },
                driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
            );

            // Run 1: Baseline
            var source1 = GenerateSource(SourceVariation.Default);
            var syntaxTree1 = CSharpSyntaxTree.ParseText(source1);
            
            var sources = Enumerable.Empty<string>()
                .Concat(Consts.ContractsSource)
                .Concat(Consts.ExtensionsSource)
                .Select(s => CSharpSyntaxTree.ParseText(s))
                .Prepend(syntaxTree1)
                .ToArray();
            
            var compilation1 = (Compilation)CSharpCompilation.Create
            (
                "compilation",
                sources,
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            
            

            driver = driver.RunGenerators(compilation1);

            // Run 2: Trivial Change (Add Comment)
            var syntaxTree2 = CSharpSyntaxTree.ParseText(GenerateSource(SourceVariation.AddComment));
            // Chain the change from compilation1
            // var compilation2 = compilation1.ReplaceSyntaxTree(compilation1.SyntaxTrees.First(), syntaxTree2);
            var compilation2 = compilation1.ReplaceSyntaxTree(syntaxTree1, syntaxTree2);
            Assert.True(compilation2.SyntaxTrees.Contains(syntaxTree2));
            Assert.False(compilation2.SyntaxTrees.Contains(syntaxTree1));

            driver = driver.RunGenerators(compilation2);
            var result2 = driver.GetRunResult();
            var trackedSteps2 = result2.Results[0].TrackedSteps;

            // This assertion should still pass
            foreach (var (stepName, steps) in trackedSteps2)
            {
                // if (!stepName.EndsWith("TypesWithGenerateSerializerAttribute")) continue;
                if (!stepWhitelist.Contains(stepName))
                {
                    continue;
                }
                Assert.All
                (
                    steps.SelectMany(s => s.Outputs),
                    o =>
                        Assert.True(o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged)
                );
            }

            // Run 3: Significant Change (Change Property Name)
            var syntaxTree3 = CSharpSyntaxTree.ParseText(GenerateSource(SourceVariation.ChangePropertyName));
            var compilation3 = compilation2.ReplaceSyntaxTree(syntaxTree2, syntaxTree3);
            Assert.True(compilation3.SyntaxTrees.Contains(syntaxTree3));
            Assert.False(compilation3.SyntaxTrees.Contains(syntaxTree2));

            driver = driver.RunGenerators(compilation3);
            var result3 = driver.GetRunResult();
            var trackedSteps3 = result3.Results[0].TrackedSteps;

            var anyModified = false;
            foreach (var (stepName, steps) in trackedSteps3)
            {
                // if (stepName.EndsWith("Output")) continue;
                if (!stepWhitelist.Contains(stepName))
                {
                    continue;
                }
                
                if (!anyModified)
                {
                    anyModified = steps.SelectMany(s => s.Outputs).Any(o => o.Reason == IncrementalStepRunReason.Modified);
                }
            }
            
            Assert.True(anyModified, "Expected at least one output to be modified in the entire pipeline");
        }

        // Generates the source
        private static string GenerateSource(SourceVariation varriation)
        {
            var comment = varriation == SourceVariation.AddComment ? " // This is a value" : string.Empty;
            var propertyName = varriation == SourceVariation.ChangePropertyName ? "ChangedValue" : "Value";

            return
                $@"using FourSer.Contracts;

namespace MyCode
{{
    [GenerateSerializer]
    public partial class SimplePacket
    {{
        {comment}
        public int {propertyName} {{ get; set; }}
    }}
}}";
        }

        private enum SourceVariation
        {
            Default,
            AddComment,
            ChangePropertyName
        }
    }
}
