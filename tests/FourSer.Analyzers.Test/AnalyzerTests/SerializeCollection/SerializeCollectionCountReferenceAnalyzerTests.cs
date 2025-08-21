using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionCountReferenceAnalyzerTests
{
    [Fact]
    public async Task NotFound_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1004:CountSizeReference = ""NonExistent""|})]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionCountReferenceAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
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
    [SerializeCollection({|#0:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public string Size { get; set; }
}";

        var expected1 = new DiagnosticResult(SerializeCollectionCountReferenceAnalyzer.WrongTypeRule).WithLocation(0).WithArguments("Size");
        var expected2 = new DiagnosticResult(SerializeCollectionCountReferenceAnalyzer.DeclaredAfterRule).WithLocation(0).WithArguments("Size");

        await new CSharpAnalyzerTest<SerializeCollectionCountReferenceAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { testCode },
                ExpectedDiagnostics = { expected1, expected2 }
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
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
    [SerializeCollection({|FSG1006:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionCountReferenceAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
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
    public int Size { get; set; }
    [SerializeCollection(CountSizeReference = ""Size"")]
    public List<int> A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionCountReferenceAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}