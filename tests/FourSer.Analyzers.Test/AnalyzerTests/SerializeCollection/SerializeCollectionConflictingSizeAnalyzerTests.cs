using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionConflictingSizeAnalyzerTests
{
    [Theory]
    [InlineData(@"
    [SerializeCollection(Unlimited = true, {|FSG1001:CountSize = 10|})]
    public List<int> A { get; set; }
")]
    [InlineData(@"
    [SerializeCollection(Unlimited = true, {|FSG1003:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
")]
    [InlineData(@"
    [SerializeCollection(CountSize = 10, {|FSG1002:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
")]
    public async Task ConflictingSizeAttributes_ReportsDiagnostic(string propertyDeclaration)
    {
        var testCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{{
{propertyDeclaration}
}}";

        await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
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
    [SerializeCollection(CountSize = 10)]
    public List<int> A { get; set; }

    [SerializeCollection(CountSizeReference = ""Size"")]
    public List<int> B { get; set; }
    public int Size { get; set; }

    [SerializeCollection(Unlimited = true)]
    public List<int> C { get; set; }
}";
        await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}