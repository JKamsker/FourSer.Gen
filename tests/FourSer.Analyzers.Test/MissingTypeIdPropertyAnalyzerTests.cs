using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class MissingTypeIdPropertyAnalyzerTests
    {
        private const string AttributeSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
        public string TypeIdProperty { get; set; }
    }
}";

        [Fact]
        public async Task SingleTypeId_WithTypeIdProperty_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = ""P"")] public List<int> L {get;set;} public int P {get;set;} }";
            await new CSharpAnalyzerTest<MissingTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task SingleTypeId_WithoutTypeIdProperty_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
class MyData { [{|FS0014:SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)|}] public List<int> L {get;set;} }";
            await new CSharpAnalyzerTest<MissingTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task NotSingleTypeId_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)] public List<int> L {get;set;} }";
            await new CSharpAnalyzerTest<MissingTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
