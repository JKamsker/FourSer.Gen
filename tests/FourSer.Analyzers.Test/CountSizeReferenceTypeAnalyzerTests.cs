using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class CountSizeReferenceTypeAnalyzerTests
    {
        private const string AttributeSource = @"
using System;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public Type CountType { get; set; }
        public int CountSize { get; set; } = -1;
        public string CountSizeReference { get; set; }
    }
}";

        [Fact]
        public async Task CountSizeReference_ToIntegerProperty_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
class MyData
{
    public int MyCount { get; set; }

    [SerializeCollection(CountSizeReference = ""MyCount"")]
    public List<int> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<CountSizeReferenceTypeAnalyzer, DefaultVerifier>
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
        public async Task CountSizeReference_ToByteProperty_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
class MyData
{
    public byte MyCount { get; set; }

    [SerializeCollection(CountSizeReference = ""MyCount"")]
    public List<int> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<CountSizeReferenceTypeAnalyzer, DefaultVerifier>
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
        public async Task CountSizeReference_ToNonIntegerProperty_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
class MyData
{
    public string {|FS0007:MyCount|} { get; set; }

    [SerializeCollection(CountSizeReference = ""MyCount"")]
    public List<int> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<CountSizeReferenceTypeAnalyzer, DefaultVerifier>
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
