using FourSer.Analyzers.General;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.Basic;

public class MissingPartialAnalyzerTests : AnalyzerTestBase
{
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
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies
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
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies
        };
        await test.RunAsync();
    }
}