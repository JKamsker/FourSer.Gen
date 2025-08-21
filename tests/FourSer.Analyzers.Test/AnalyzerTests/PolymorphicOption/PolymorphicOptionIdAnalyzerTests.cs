using System.Collections.Immutable;
using FourSer.Analyzers.PolymorphicOption;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionIdAnalyzerTests
{
    [Theory]
    [InlineData(@"[PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3000:10|}, typeof(string))]")]
    [InlineData(@"[PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3001:(byte)20|}, typeof(string))]")]
    public async Task InvalidIdCombinations_ReportsDiagnostic(string attributes)
    {
        var testCode = @$"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{{
    {attributes}
    public object A {{ get; set; }}
}}";
        await new CSharpAnalyzerTest<PolymorphicOptionIdAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }

    [Fact]
    public async Task ValidUsage_NoDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption(20, typeof(string))]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<PolymorphicOptionIdAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}