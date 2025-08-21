using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionOnCorrectMemberAnalyzerTests
{
    [Fact]
    public async Task OnNonIEnumerable_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [{|#0:SerializeCollection|}]
    public int A { get; set; }
}";
        var expected = new DiagnosticResult(SerializeCollectionOnCorrectMemberAnalyzer.Rule).WithLocation(0);
        await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { testCode },
                ExpectedDiagnostics = { expected },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Theory]
    [InlineData("IEnumerable<int>")]
    [InlineData("List<int>")]
    [InlineData("int[]")]
    public async Task OnCollectionTypes_NoDiagnostic(string collectionType)
    {
        var testCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{{
    [SerializeCollection]
    public {collectionType} A {{ get; set; }}
}}";
        await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}