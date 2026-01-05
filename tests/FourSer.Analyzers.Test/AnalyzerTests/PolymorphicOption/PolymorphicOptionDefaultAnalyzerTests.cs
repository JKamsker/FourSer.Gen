using FourSer.Analyzers.PolymorphicOption;
using FourSer.Analyzers.Test.Helpers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionDefaultAnalyzerTests : AnalyzerTestBase
{
    [Fact]
    public async Task MultipleDefaults_ReportDiagnostics()
    {
        const string source = """
        #nullable enable
        using System;

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
        public sealed class PolymorphicOptionAttribute : Attribute
        {
            public PolymorphicOptionAttribute(int id, Type type, bool isDefault = false) { }
        }

        public class Example
        {
            [PolymorphicOption(1, typeof(int), isDefault: true)]
            [PolymorphicOption(2, typeof(string), {|FSG3003:isDefault: true|})]
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
        #nullable enable
        using System;

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
        public sealed class PolymorphicOptionAttribute : Attribute
        {
            public PolymorphicOptionAttribute(int id, Type type, bool isDefault = false) { }
        }

        public class Example
        {
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
