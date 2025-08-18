using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class InvalidCountTypeAnalyzerTests
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
        public Type CountType { get; set; }
    }
}";

        [Fact]
        public async Task ValidIntCountType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [SerializeCollection(CountType = typeof(int))] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<InvalidCountTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task ValidByteCountType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [SerializeCollection(CountType = typeof(byte))] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<InvalidCountTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task InvalidStringCountType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [{|FS0012:SerializeCollection(CountType = typeof(string))|}] public int[] MyList { get; set; } }";
            await new CSharpAnalyzerTest<InvalidCountTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
