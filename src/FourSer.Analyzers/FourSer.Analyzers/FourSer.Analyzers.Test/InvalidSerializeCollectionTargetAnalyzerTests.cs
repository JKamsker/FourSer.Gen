using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class InvalidSerializeCollectionTargetAnalyzerTests
    {
        private const string AttributeSource = @"
using System;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public Type CountType { get; set; }
        public int CountSize { get; set; } = -1;
        public string CountSizeReference { get; set; }
    }
}";

        [Fact]
        public async Task SerializeCollection_OnCollectionType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

class MyData
{
    [SerializeCollection]
    public List<int> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidSerializeCollectionTargetAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributeSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task SerializeCollection_OnNonCollectionType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

class MyData
{
    [SerializeCollection]
    public int {|FS0005:MyValue|} { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidSerializeCollectionTargetAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributeSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }
    }
}
