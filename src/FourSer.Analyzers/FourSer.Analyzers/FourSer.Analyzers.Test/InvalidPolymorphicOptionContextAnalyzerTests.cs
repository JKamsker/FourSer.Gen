using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class InvalidPolymorphicOptionContextAnalyzerTests
    {
        private const string AttributeSource = @"
using System;

namespace FourSer.Contracts
{
    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(int id, Type type) { }
    }

    public class BaseType { }
    public class DerivedType : BaseType { }
}";

        [Fact]
        public async Task PolymorphicOption_WithSerializePolymorphic_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

class MyData
{
    [SerializePolymorphic]
    [PolymorphicOption(1, typeof(DerivedType))]
    public BaseType MyProperty { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicOptionContextAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_WithSerializeCollection_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)]
    [PolymorphicOption(1, typeof(DerivedType))]
    public List<BaseType> MyList { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicOptionContextAnalyzer, DefaultVerifier>
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
        public async Task PolymorphicOption_WithoutContext_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

class MyData
{
    [PolymorphicOption(1, typeof(DerivedType))]
    public BaseType {|FS0008:MyProperty|} { get; set; }
}";

            var test = new CSharpAnalyzerTest<InvalidPolymorphicOptionContextAnalyzer, DefaultVerifier>
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
