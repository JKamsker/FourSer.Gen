using System.Threading.Tasks;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class CountSizeReferenceOrderAnalyzerTests
    {
        private const string AttributeSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public string CountSizeReference { get; set; }
    }
}";

        [Fact]
        public async Task CountSizeReference_DeclaredBeforeCollection_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
public partial class Inventory
{
    public int Count { get; set; }

    [SerializeCollection(CountSizeReference = nameof(Count))]
    public List<int> Items { get; set; }
}";

            await new CSharpAnalyzerTest<CountSizeReferenceOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributeSource, testCode },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CountSizeReference_DeclaredAfterCollection_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(CountSizeReference = nameof(Count))]
    public List<int> {|FS0007:Items|} { get; set; }

    public int Count { get; set; }
}";

            await new CSharpAnalyzerTest<CountSizeReferenceOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributeSource, testCode },
                }
            }.RunAsync();
        }
    }
}