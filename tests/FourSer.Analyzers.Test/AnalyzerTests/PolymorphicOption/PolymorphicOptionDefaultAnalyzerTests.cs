using FourSer.Analyzers.PolymorphicOption;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionDefaultAnalyzerTests : AnalyzerTestBase
{
    [Fact]
    public async Task MultipleDefaults_ReportDiagnostics()
    {
        const string source = """
        using FourSer.Contracts;

        public class Example
        {
            [SerializePolymorphic]
            [PolymorphicOption(1, typeof(int), isDefault: true)]
            [PolymorphicOption(2, typeof(string), {|FSG3002:isDefault: true|})]
            public object? Value { get; set; }
        }
        """;

        await new CSharpAnalyzerTest<PolymorphicOptionDefaultAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
            TestCode = source
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
            [PolymorphicOption(1, typeof(int), isDefault: true)]
            [PolymorphicOption(2, typeof(string))]
            public object? Value { get; set; }
        }
        """;

        await new CSharpAnalyzerTest<PolymorphicOptionDefaultAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies,
            TestCode = source
        }.RunAsync();
    }
}
