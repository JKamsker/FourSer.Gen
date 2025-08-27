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
            var source = @"
using FourSer.Contracts;

namespace MyCode
{
    [GenerateSerializer]
    public partial class SimplePacket
    {
        public int Value { get; set; }
    }
}
";

            var compilation = (Compilation)CSharpCompilation.Create(
                "compilation",
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            var generator = new SerializerGenerator();
            var sourceGenerator = generator.AsSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { sourceGenerator },
                driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)
            );

            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();

            var compilation2 = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// dummy change"));
            driver = driver.RunGenerators(compilation2);
            var result2 = driver.GetRunResult();

            var trackedSteps = result2.Results[0].TrackedSteps;

            foreach (var (stepName, steps) in trackedSteps)
            {
                if (stepName.EndsWith("Output")) continue;

                Assert.All(steps.SelectMany(s => s.Outputs), o =>
                    Assert.True(o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged)
                );
            }
        }
    }
}
