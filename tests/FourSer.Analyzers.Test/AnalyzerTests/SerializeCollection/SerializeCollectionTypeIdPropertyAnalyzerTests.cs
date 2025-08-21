using FourSer.Analyzers.SerializeCollection;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionTypeIdPropertyAnalyzerTests : AnalyzerTestBase
{
    [Fact]
    public async Task NotFound_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1007:TypeIdProperty = ""NonExistent""|})]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }

    [Fact]
    public async Task WrongType_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|#0:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public float TypeId { get; set; }
}";

        var expected1 = new DiagnosticResult(SerializeCollectionTypeIdPropertyAnalyzer.WrongTypeRule).WithLocation(0).WithArguments("TypeId");
        var expected2 = new DiagnosticResult(SerializeCollectionTypeIdPropertyAnalyzer.DeclaredAfterRule).WithLocation(0).WithArguments("TypeId");

        await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { testCode },
                ExpectedDiagnostics = { expected1, expected2 }
            },
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }

    [Fact]
    public async Task DeclaredAfter_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1009:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }

    [Fact]
    public async Task ValidUsage_NoDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"")]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }
}