using System.Threading.Tasks;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionCountReferenceCodeFixProviderTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public string CountSizeReference { get; set; }
    }
}";

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
}";

            var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int Size { get; set; }
    [SerializeCollection(CountSizeReference = ""Size"")]
    public List<int> A { get; set; }
}";

            await new CSharpCodeFixTest<SerializeCollectionCountReferenceAnalyzer, SerializeCollectionCountReferenceCodeFixProvider, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
                FixedState = { Sources = { AttributesSource, fixedCode } },
            }.RunAsync();
        }
    }
}
