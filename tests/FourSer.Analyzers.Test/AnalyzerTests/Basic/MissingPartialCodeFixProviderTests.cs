using System.Collections.Immutable;
using FourSer.Analyzers.General;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.Basic;

public class MissingPartialCodeFixProviderTests
{
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
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        };
        await test.RunAsync();
    }
}