using System.Collections.Immutable;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionConflictingSizeCodeFixProviderTests
{
    [Theory]
    [InlineData(
        @"    [SerializeCollection(Unlimited = true, {|FSG1001:CountSize = 10|})]",
        @"    [SerializeCollection(Unlimited = true)]")]
    [InlineData(
        @"    [SerializeCollection(Unlimited = true, {|FSG1003:CountSizeReference = ""Size""|})]",
        @"    [SerializeCollection(Unlimited = true)]")]
    [InlineData(
        @"    [SerializeCollection(CountSize = 10, {|FSG1002:CountSizeReference = ""Size""|})]",
        @"    [SerializeCollection(CountSize = 10)]")]
    public async Task ConflictingSizeAttributes_RemovesConflictingArgument(string testAttribute, string fixedAttribute)
    {
        var testCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{{
{testAttribute}
    public List<int> A {{ get; set; }}
    public int Size {{ get; set; }}
}}";

        var fixedCode = @$"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{{
{fixedAttribute}
    public List<int> A {{ get; set; }}
    public int Size {{ get; set; }}
}}";

        await new CSharpCodeFixTest<SerializeCollectionConflictingSizeAnalyzer, SerializeCollectionConflictingSizeCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90.AddPackages(ImmutableArray.Create(new PackageIdentity("FourSer.Gen", "0.0.164")))
        }.RunAsync();
    }
}