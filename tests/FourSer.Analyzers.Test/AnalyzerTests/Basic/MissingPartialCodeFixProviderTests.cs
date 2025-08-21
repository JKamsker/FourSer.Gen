using FourSer.Analyzers.General;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.Basic;

public class MissingPartialCodeFixProviderTests
{
    private const string GenerateSerializerAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : System.Attribute { }
}";

    [Fact]
    public async Task ClassWithGenerateSerializer_MissingPartial_AddsPartialModifier()
    {
        var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
class {|FS0001:MyData|}
{
    public int A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;

[GenerateSerializer]
partial class MyData
{
    public int A { get; set; }
}";

        var test = new CSharpCodeFixTest<MissingPartialAnalyzer, MissingPartialCodeFixProvider, DefaultVerifier>
        {
            TestState =
            {
                Sources = { GenerateSerializerAttributeSource, testCode },
            },
            FixedState =
            {
                Sources = { GenerateSerializerAttributeSource, fixedCode },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };
        await test.RunAsync();
    }
}