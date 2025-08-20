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
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : Attribute { }

    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;
        public string TypeIdProperty { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(byte id, System.Type type) { }
    }
}";

        [Fact]
        public async Task SingleTypeId_WithTypeIdProperty_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;
[GenerateSerializer]
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
[GenerateSerializer]
class MyData { [SerializeCollection({|FS0014:PolymorphicMode = PolymorphicMode.SingleTypeId|})] public List<int> L {get;set;} }";
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
[GenerateSerializer]
class MyData { [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds)] public List<int> L {get;set;} }";
            await new CSharpAnalyzerTest<MissingTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task SingleTypeId_WithPolymorphicOptions_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
    [PolymorphicOption(10, typeof(Sword))]
    [PolymorphicOption(20, typeof(Shield))]
    [PolymorphicOption(30, typeof(Potion))]
    public IEnumerable<Item> Items { get; set; } = new List<Item>();
}

[GenerateSerializer] public partial class Item { }
[GenerateSerializer] public partial class Sword : Item { }
[GenerateSerializer] public partial class Shield : Item { }
[GenerateSerializer] public partial class Potion : Item { }
";
            await new CSharpAnalyzerTest<MissingTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributeSource, testCode } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
