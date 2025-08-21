using System.Collections.Immutable;
using FourSer.Analyzers.SerializePolymorphic;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializePolymorphic;

public class SerializePolymorphicPropertyNameAnalyzerTests
{
    [Fact]
    public async Task NotFound_ReportsDiagnostic()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializePolymorphic({|FSG2000:""NonExistent""|})]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializePolymorphicPropertyNameAnalyzer, DefaultVerifier>
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
    [SerializePolymorphic({|#0:""TypeId""|})]
    public object A { get; set; }
    public float TypeId { get; set; }
}";
        var expected1 = new DiagnosticResult(SerializePolymorphicPropertyNameAnalyzer.WrongTypeRule).WithLocation(0).WithArguments("TypeId");
        var expected2 = new DiagnosticResult(SerializePolymorphicPropertyNameAnalyzer.DeclaredAfterRule).WithLocation(0).WithArguments("TypeId");
        await new CSharpAnalyzerTest<SerializePolymorphicPropertyNameAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode }, ExpectedDiagnostics = { expected1, expected2 } },
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
    [SerializePolymorphic({|FSG2002:""TypeId""|})]
    public object A { get; set; }
    public int TypeId { get; set; }
}";
        await new CSharpAnalyzerTest<SerializePolymorphicPropertyNameAnalyzer, DefaultVerifier>
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
    public int TypeId { get; set; }
    [SerializePolymorphic(""TypeId"")]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<SerializePolymorphicPropertyNameAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}