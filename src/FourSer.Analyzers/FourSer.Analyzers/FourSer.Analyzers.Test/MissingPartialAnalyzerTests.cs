using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class MissingPartialAnalyzerTests
    {
        private const string GenerateSerializerAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : System.Attribute { }
}";

        [Fact]
        public async Task ClassWithGenerateSerializer_MissingPartial_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
class {|FS0001:MyData|}
{
    public int A { get; set; }
}";
            var test = new CSharpAnalyzerTest<MissingPartialAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task ClassWithGenerateSerializer_WithPartial_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
partial class MyData
{
    public int A { get; set; }
}";

            var test = new CSharpAnalyzerTest<MissingPartialAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }
    }
}
