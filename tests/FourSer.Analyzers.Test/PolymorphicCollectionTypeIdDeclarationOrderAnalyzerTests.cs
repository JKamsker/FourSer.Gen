using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using FourSer.Analyzers;

namespace FourSer.Analyzers.Test
{
    public class PolymorphicCollectionTypeIdDeclarationOrderAnalyzerTests
    {
        private const string ContractsSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : System.Attribute { }

    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
    public class SerializeCollectionAttribute : System.Attribute
    {
        public PolymorphicMode PolymorphicMode { get; set; }
        public string TypeIdProperty { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : System.Attribute
    {
        public PolymorphicOptionAttribute(int id, System.Type type) { }
    }
}";

        [Fact]
        public async Task TypeIdDeclaredAfterCollection_ReportsDiagnostic()
        {
            var testCode = @"
using System.Collections.Generic;
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = nameof(TypeId))]
    [PolymorphicOption(1, typeof(Nested))]
    public List<object> {|FSSG002:Items|} { get; set; }

    public int TypeId { get; set; }
}
public class Nested {}
";

            var test = new CSharpAnalyzerTest<PolymorphicCollectionTypeIdDeclarationOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { ContractsSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }

        [Fact]
        public async Task TypeIdDeclaredBeforeCollection_NoDiagnostic()
        {
            var testCode = @"
using System.Collections.Generic;
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    public int TypeId { get; set; }

    [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdProperty = nameof(TypeId))]
    [PolymorphicOption(1, typeof(Nested))]
    public List<object> Items { get; set; }
}
public class Nested {}
";

            var test = new CSharpAnalyzerTest<PolymorphicCollectionTypeIdDeclarationOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { ContractsSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }
        
        [Fact]
        public async Task TypeIdDeclaredAfterCollection_ReportsDiagnostic_ImplicitTypeId()
        {
            var testCode = @"
using System.Collections.Generic;
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializeCollection(TypeIdProperty = nameof(TypeId))]
    [PolymorphicOption(1, typeof(Nested))]
    public List<object> {|FSSG002:Items|} { get; set; }

    public int TypeId { get; set; }
}
public class Nested {}
";

            var test = new CSharpAnalyzerTest<PolymorphicCollectionTypeIdDeclarationOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { ContractsSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        }
        
        // TypeIdProperty specified, but property is not declared
        [Fact]
        public async Task TypeIdPropertyNotDeclared_ReportsDiagnostic()
        {
            var testCode = @"
using System.Collections.Generic;
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializeCollection(TypeIdProperty = ""TypeId"")]
    [PolymorphicOption(1, typeof(Nested))]
    public List<object> {|FSSG018:Items|} { get; set; }
}
public class Nested {}
";

            var test = new CSharpAnalyzerTest<PolymorphicCollectionTypeIdDeclarationOrderAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { ContractsSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            };
            await test.RunAsync();
        } 
    }
}
