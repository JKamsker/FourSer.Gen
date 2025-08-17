using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test
{
    public class PolymorphicOptionAnalyzerTests
    {
        private const string GenerateSerializerAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class GenerateSerializerAttribute : System.Attribute { }
}";

        private const string SerializePolymorphicAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : System.Attribute
    {
        public string? PropertyName { get; set; }
        public System.Type? TypeIdType { get; set; }

        public SerializePolymorphicAttribute(string? propertyName = null)
        {
            PropertyName = propertyName;
        }
    }
}";

        private const string PolymorphicOptionAttributeSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : System.Attribute
    {
        public object Id { get; }
        public System.Type Type { get; }

        public PolymorphicOptionAttribute(int id, System.Type type) { }
        public PolymorphicOptionAttribute(byte id, System.Type type) { }
        public PolymorphicOptionAttribute(ushort id, System.Type type) { }
        public PolymorphicOptionAttribute(long id, System.Type type) { }
        public PolymorphicOptionAttribute(object id, System.Type type) { }
    }
}";

        private const string BaseClassesSource = @"
public class BaseEntity { }
public class EntityType1 : BaseEntity { }
public class EntityType2 : BaseEntity { }
";

        [Fact]
        public async Task MismatchedOptionTypes_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializePolymorphic(TypeIdType = typeof(int))]
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption((byte)2, typeof(EntityType2))]
    public BaseEntity MyProperty { get; set; }
}
" + BaseClassesSource;

            var expected = new DiagnosticResult(PolymorphicOptionAnalyzer.MismatchedTypesRule)
                .WithSpan("/0/Test3.cs", 9, 6, 9, 53)
                .WithArguments("MyProperty", "Int32", "Byte");

            var test = new CSharpAnalyzerTest<PolymorphicOptionAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, SerializePolymorphicAttributeSource, PolymorphicOptionAttributeSource, testCode },
                    ExpectedDiagnostics = { expected },
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task OptionTypeMismatchWithTypeIDType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption(1, typeof(EntityType1))]
    [PolymorphicOption(2, typeof(EntityType2))]
    public BaseEntity MyProperty { get; set; }
}
" + BaseClassesSource;

            var expected = new DiagnosticResult(PolymorphicOptionAnalyzer.Rule)
                .WithSpan("/0/Test3.cs", 7, 6, 7, 53)
                .WithArguments("Int32", "Byte");

            var test = new CSharpAnalyzerTest<PolymorphicOptionAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, SerializePolymorphicAttributeSource, PolymorphicOptionAttributeSource, testCode },
                    ExpectedDiagnostics = { expected },
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task ValidPolymorphicOptions_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

[GenerateSerializer]
public partial class MyData
{
    [SerializePolymorphic(TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)1, typeof(EntityType1))]
    [PolymorphicOption((byte)2, typeof(EntityType2))]
    public BaseEntity MyProperty { get; set; }
}
" + BaseClassesSource;

            var test = new CSharpAnalyzerTest<PolymorphicOptionAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { GenerateSerializerAttributeSource, SerializePolymorphicAttributeSource, PolymorphicOptionAttributeSource, testCode },
                },
            };

            await test.RunAsync();
        }
    }
}
