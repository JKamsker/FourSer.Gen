using System.Threading.Tasks;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class CountSizeReferenceExistenceAnalyzerTests
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
        public async Task CountSizeReference_ToExistingProperty_NoDiagnostic()
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

            var test = new CSharpAnalyzerTest<CountSizeReferenceExistenceAnalyzer, DefaultVerifier>
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
        public async Task CountSizeReference_ToNonExistentProperty_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
class MyData
{
    [SerializeCollection({|FS0006:CountSizeReference = ""NonExistentCount""|})]
    public List<int> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<CountSizeReferenceExistenceAnalyzer, DefaultVerifier>
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
