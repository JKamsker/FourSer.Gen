using System.Collections.Immutable;
using FourSer.Analyzers.PolymorphicOption;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionIdCodeFixProviderTests
{
    [Fact]
    public async Task DuplicateIds_RemovesDuplicate()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3000:10|}, typeof(string))]
    public object A { get; set; }
}";

        var fixedCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    public object A { get; set; }
}";

        await new CSharpCodeFixTest<PolymorphicOptionIdAnalyzer, PolymorphicOptionIdCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}