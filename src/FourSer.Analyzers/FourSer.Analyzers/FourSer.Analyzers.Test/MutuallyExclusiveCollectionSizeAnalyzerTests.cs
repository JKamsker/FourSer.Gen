using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class MutuallyExclusiveCollectionSizeAnalyzerTests
    {
        private const string AttributeSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public int CountSize { get; set; } = -1;
        public string CountSizeReference { get; set; }
        public bool Unlimited { get; set; }
    }
}";

        [Fact]
        public async Task OnlyCountSize_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [SerializeCollection(CountSize = 10)] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task OnlyCountSizeReference_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [SerializeCollection(CountSizeReference = ""C"")] public int[] MyList { get; set; } public int C {get;set;} }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task OnlyUnlimited_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [SerializeCollection(Unlimited = true)] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task CountSizeAndCountSizeReference_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [{|FS0011:SerializeCollection(CountSize = 10, CountSizeReference = ""C"")|}] public int[] MyList { get; set; } public int C {get;set;} }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task CountSizeAndUnlimited_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [{|FS0011:SerializeCollection(CountSize = 10, Unlimited = true)|}] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task AllThree_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
class MyData { [{|FS0011:SerializeCollection(CountSize = 10, CountSizeReference = ""C"", Unlimited = true)|}] public int[] MyList { get; set; } public int C {get;set;} }";
            await new CSharpAnalyzerTest<MutuallyExclusiveCollectionSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
