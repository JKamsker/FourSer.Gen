using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class MismatchedTypeIdPropertyTypeAnalyzerTests
    {
        private const string AttributeSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
        public string TypeIdProperty { get; set; }
        public Type TypeIdType { get; set; }
    }
}";

        [Fact]
        public async Task MatchingTypes_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
[GenerateSerializer]
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = ""P"", TypeIdType = typeof(byte))] public List<int> L {get;set;} public byte P {get;set;} }";
            await new CSharpAnalyzerTest<MismatchedTypeIdPropertyTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task DefaultIntType_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
[GenerateSerializer]
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = ""P"")] public List<int> L {get;set;} public int P {get;set;} }";
            await new CSharpAnalyzerTest<MismatchedTypeIdPropertyTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task MismatchedTypes_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
[GenerateSerializer]
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = ""P"", TypeIdType = typeof(byte))] public List<int> L {get;set;} public int {|FS0015:P|} {get;set;} }";
            await new CSharpAnalyzerTest<MismatchedTypeIdPropertyTypeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
