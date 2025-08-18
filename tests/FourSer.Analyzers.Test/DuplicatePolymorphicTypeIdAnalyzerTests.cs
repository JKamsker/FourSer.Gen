using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class DuplicatePolymorphicTypeIdAnalyzerTests
    {
        private const string AttributeSource = @"
using System;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(int id, Type type) { }
    }

    public class BaseType { }
    public class DerivedType1 : BaseType { }
    public class DerivedType2 : BaseType { }
}";

        [Fact]
        public async Task UniqueTypeIds_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
class MyData
{
    [PolymorphicOption(1, typeof(DerivedType1))]
    [PolymorphicOption(2, typeof(DerivedType2))]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<DuplicatePolymorphicTypeIdAnalyzer, DefaultVerifier>
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
        public async Task DuplicateTypeIds_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
class MyData
{
    [PolymorphicOption(1, typeof(DerivedType1))]
    [{|FS0010:PolymorphicOption(1, typeof(DerivedType2))|}]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<DuplicatePolymorphicTypeIdAnalyzer, DefaultVerifier>
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
