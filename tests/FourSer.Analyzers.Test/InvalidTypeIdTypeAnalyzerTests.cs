using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class InvalidTypeIdTypeAnalyzerTests
    {
        private const string AttributeSource = @"
using System;
namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : Attribute { public Type TypeIdType { get; set; } }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute { public Type TypeIdType { get; set; } }

    public enum MyEnum { A, B }
}";

        [Fact]
        public async Task ValidIntegerTypeIdType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [SerializePolymorphic(TypeIdType = typeof(int))] public object MyProp { get; set; } }";
            await new CSharpAnalyzerTest<InvalidTypeIdTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task ValidEnumTypeIdType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [SerializeCollection(TypeIdType = typeof(MyEnum))] public object MyProp { get; set; } }";
            await new CSharpAnalyzerTest<InvalidTypeIdTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task InvalidStringTypeIdType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
[GenerateSerializer]
class MyData { [{|FS0013:SerializePolymorphic(TypeIdType = typeof(string))|}] public object MyProp { get; set; } }";
            await new CSharpAnalyzerTest<InvalidTypeIdTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
