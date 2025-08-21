using FourSer.Analyzers.SerializeCollection;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionOnCorrectMemberCodeFixProviderTests : AnalyzerTestBase
{
    [Theory]
    [InlineData(0, "public int A { get; set; }")]
    [InlineData(1, "[SerializePolymorphic]\n    public int A { get; set; }")]
    public async Task CodeFix_ProvidesCorrectFix(int codeActionIndex, string expectedProperty)
    {
        var testCode = @"
using FourSer.Contracts;

public class MyData
{
    [{|FSG1000:SerializeCollection|}]
    public int A { get; set; }
}";

        var fixedCode = @$"
using FourSer.Contracts;

public class MyData
{{
    {expectedProperty}
}}";

        await new CSharpCodeFixTest<SerializeCollectionOnCorrectMemberAnalyzer, SerializeCollectionOnCorrectMemberCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            CodeActionIndex = codeActionIndex,
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }
}