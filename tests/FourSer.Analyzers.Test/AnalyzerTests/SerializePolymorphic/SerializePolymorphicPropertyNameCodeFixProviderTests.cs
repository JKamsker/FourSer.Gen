using System.Collections.Immutable;
using FourSer.Analyzers.SerializePolymorphic;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializePolymorphic;

public class SerializePolymorphicPropertyNameCodeFixProviderTests
{
    [Fact]
    public async Task NotFound_CreatesProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializePolymorphic({|FSG2000:""TypeId""|})]
    public object A { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializePolymorphic(""TypeId"")]
    public object A { get; set; }
}";

        await new CSharpCodeFixTest<SerializePolymorphicPropertyNameAnalyzer, SerializePolymorphicPropertyNameCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}
