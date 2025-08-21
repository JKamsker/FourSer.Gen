using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionPolymorphismCodeFixProviderTests
{
    [Fact]
    public async Task IndividualTypeIdsWithTypeIdProperty_RemovesTypeIdProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, {|FSG1011:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";

        await new CSharpCodeFixTest<SerializeCollectionPolymorphismAnalyzer, SerializeCollectionPolymorphismCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}