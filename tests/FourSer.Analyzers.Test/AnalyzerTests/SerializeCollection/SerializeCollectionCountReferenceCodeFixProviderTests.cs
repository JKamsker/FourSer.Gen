using System;
using FourSer.Analyzers.SerializeCollection;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection;

public class SerializeCollectionCountReferenceCodeFixProviderTests : AnalyzerTestBase
{
    [Fact]
    public async Task NotFound_CreatesProperty()
    {
        var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1004:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
}".ReplaceLineEndings(Environment.NewLine);

        var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int Size { get; set; }
    [SerializeCollection(CountSizeReference = ""Size"")]
    public List<int> A { get; set; }
}".ReplaceLineEndings(Environment.NewLine);

        await new CSharpCodeFixTest<SerializeCollectionCountReferenceAnalyzer, SerializeCollectionCountReferenceCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { testCode } },
            FixedState = { Sources = { fixedCode } },
            ReferenceAssemblies = ReferenceAssemblies
        }.RunAsync();
    }
}
