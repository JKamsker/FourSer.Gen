using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class InvalidPolymorphicTypeAnalyzerTests
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

    [GenerateSerializer]
    public partial class BaseType { }

    [GenerateSerializer]
    public partial class DerivedType : BaseType { }

    [GenerateSerializer]
    public partial class UnrelatedType { }

    public class AssignableButNotSerializable : BaseType { }
}";

        [Fact]
        public async Task PolymorphicOption_WithAssignableType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

class MyData
{
    [PolymorphicOption(1, typeof(DerivedType))]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicTypeAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_OnListWithAssignableType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

class MyData
{
    [PolymorphicOption(1, typeof(DerivedType))]
    public List<BaseType> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicTypeAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_WithoutGenerateSerializer_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

class MyData
{
    [{|FS0016:PolymorphicOption(1, typeof(AssignableButNotSerializable))|}]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicTypeAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_WithSameType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

class MyData
{
    [PolymorphicOption(1, typeof(BaseType))]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicTypeAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_WithUnassignableType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

class MyData
{
    [{|FS0009:PolymorphicOption(1, typeof(UnrelatedType))|}]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicTypeAnalyzer, DefaultVerifier>
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
