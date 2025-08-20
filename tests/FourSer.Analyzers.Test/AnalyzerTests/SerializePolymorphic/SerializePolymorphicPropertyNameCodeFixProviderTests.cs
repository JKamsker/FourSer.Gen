using System.Threading.Tasks;
using FourSer.Analyzers.SerializePolymorphic;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializePolymorphic
{
    public class SerializePolymorphicPropertyNameCodeFixProviderTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializePolymorphicAttribute : Attribute
    {
        public SerializePolymorphicAttribute(string propertyName) { }
    }
}";

        [Fact]
        public async Task NotFound_CreatesProperty()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializePolymorphic({|FSG2000:""TypeId""|})]
    public object A { get; set; }
}";

            var fixedCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializePolymorphic(""TypeId"")]
    public object A { get; set; }
}";

            await new CSharpCodeFixTest<SerializePolymorphicPropertyNameAnalyzer, SerializePolymorphicPropertyNameCodeFixProvider, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
                FixedState = { Sources = { AttributesSource, fixedCode } },
            }.RunAsync();
        }
    }
}
