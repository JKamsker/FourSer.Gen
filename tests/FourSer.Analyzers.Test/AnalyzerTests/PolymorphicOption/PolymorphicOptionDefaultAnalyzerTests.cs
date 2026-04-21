using FourSer.Analyzers.PolymorphicOption;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionDefaultAnalyzerTests : AnalyzerTestBase
{
    private const string ContractsSource = """
    namespace FourSer.Contracts;

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class SerializePolymorphicAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = true)]
    public sealed class PolymorphicOptionAttribute : System.Attribute
    {
        public PolymorphicOptionAttribute(int id, System.Type type)
        {
        }

        public bool IsDefault { get; set; }
    }
    """;

    [Fact]
    public async Task MultipleDefaults_ReportDiagnostics()
    {
        const string source = """
        using FourSer.Contracts;

        public class Example
        {
            [SerializePolymorphic]
            [PolymorphicOption(1, typeof(int), IsDefault = true)]
            [PolymorphicOption(2, typeof(string), {|FSG3003:IsDefault = true|})]
            public object? Value { get; set; }
        }
        """;

        await new CSharpAnalyzerTest<PolymorphicOptionDefaultAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
            TestState =
            {
                Sources = { ContractsSource, source }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task SingleDefault_NoDiagnostics()
    {
        const string source = """
        using FourSer.Contracts;

        public class Example
        {
            [SerializePolymorphic]
            [PolymorphicOption(1, typeof(int), IsDefault = true)]
            [PolymorphicOption(2, typeof(string))]
            public object? Value { get; set; }
        }
        """;

        await new CSharpAnalyzerTest<PolymorphicOptionDefaultAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
            TestState =
            {
                Sources = { ContractsSource, source }
            }
        }.RunAsync();
    }
}
