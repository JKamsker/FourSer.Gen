using FourSer.Analyzers.SerializeCollection;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionOnCorrectMemberAnalyzerTests : AnalyzerTestBase
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
            ReferenceAssemblies = ReferenceAssemblies
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
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }
}